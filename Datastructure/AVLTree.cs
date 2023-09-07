/*
    Copyright 2014 Travel Modelling Group, Department of Civil Engineering, University of Toronto

    This file is part of XTMF.

    XTMF is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    XTMF is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with XTMF.  If not, see <http://www.gnu.org/licenses/>.
*/
using System;
using System.Collections.Generic;
using System.Threading;

// We actually need to take the reference (ref) of a volatile
#pragma warning disable 420

namespace Datastructure
{
    /// <summary>
    /// Represents an AST (Self balancing Binary Search Tree)
    /// which is THREAD SAFE!
    /// Single Writer, Multiple Readers
    /// </summary>
    /// <typeparam name="T">The type of data that we want to store</typeparam>
    public class AvlTree<T>
        where T : IComparable<T>
    {
        protected readonly object WriterLock = new();
        private volatile int _Count;
        private volatile int ReaderCount;
        private volatile Node? _Root;

        /// <summary>
        /// Returns how many elements are currently in the tree
        /// </summary>
        public int Count => _Count;

        /// <summary>
        /// Returns when the _Root is synchronized
        /// This will deadlock if you hold the SynchRoot and lock it
        /// </summary>
        public bool IsSynchronized
        {
            get
            {
                lock ( WriterLock )
                {
                    return true;
                }
            }
        }

        /// <summary>
        /// If you hold this, the datastructure will be read-only
        /// </summary>
        public object SyncRoot => WriterLock;

        protected Node? Root { get { return _Root; } set { _Root = value; } }

        /// <summary>
        /// Adds a new item to the BST
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public bool Add(T item)
        {
            var visited = new Stack<Node>();
            Node? current;
            // TODO: We need to test the performance difference between making the node here or not
            // Making it here could allow more readers in, making the lag for a write take long?
            // However, if we are parallel writing, doing more work in parallel is better
            MakeNode( item, out Node temp );
            lock (WriterLock)
            {
                current = Root;
                // This is the other place to logically make the node
                //this.MakeNode(item, out temp);
                // Do as much as we can before waiting for the readers to finish
                if ( current == null )
                {
                    WaitForReaders();
                    Root = temp;
                    IncreaseCount();
                    return true;
                }
                while ( current != null )
                {
                    visited.Push( current );
                    if ( item.CompareTo( current.Data ) < 0 )
                    {
                        if ( current.Left == null )
                        {
                            current.Left = temp;
                            break;
                        }
                        else
                        {
                            current = current.Left;
                        }
                    }
                    else
                    {
                        if ( current.Right == null )
                        {
                            current.Right = temp;
                            break;
                        }
                        else
                        {
                            current = current.Right;
                        }
                    }
                }
                // We only need to wait for doing this NOW
                WaitForReaders();
                //Balance the tree now
                BalanceTree( visited );
                IncreaseCount();
                // We need to make sure all of the nodes memory it now shared between processors
                Thread.MemoryBarrier();
            }
            return true;
        }

        /// <summary>
        /// Removes all elements
        /// </summary>
        public void Clear()
        {
            lock (WriterLock)
            {
                WaitForReaders();
                Root = null;
                // free up all of the memory resources
                GC.Collect();
            }
        }

        /// <summary>
        /// Checks to see if the data exists in the AST
        /// </summary>
        /// <param name="item">What we are looking for</param>
        /// <returns>True if it is found, false otherwise</returns>
        public bool Contains(T item)
        {
            if ( item == null ) return false;
            IncreaseReaders();
            var current = Root;
            while ( current != null )
            {
                var dif = item.CompareTo( current.Data );
                if ( dif < 0 )
                {
                    current = current.Left;
                }
                else if ( dif > 0 )
                {
                    current = current.Right;
                }
                else
                {
                    break;
                }
            }
            DecreaseReaders();
            return ( current != null );
        }

        public void CopyTo(Array array, int index)
        {
            var i = 0;
            foreach ( var t in this )
            {
                array.SetValue( t, index + i );
                i++;
            }
        }

        /// <summary>
        /// Do not write to tree while Enumerating it!
        /// Or you will Deadlock.
        /// The structure will remain in a consistent state while reading
        /// </summary>
        /// <returns></returns>
        public IEnumerator<T?> GetEnumerator()
        {
            IncreaseReaders();
            var stack = new Stack<Node>();
            for ( var current = Root; current != null || stack.Count > 0; current = current.Right )
            {
                while ( current != null )
                {
                    stack.Push( current );
                    current = current.Left;
                }
                current = stack.Pop();
                yield return current.Data;
            }
            DecreaseReaders();
        }

        /// <summary>
        /// This makes no sense for a BST
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public bool Remove(out T item)
        {
            throw new NotImplementedException("Removing an item by it's data from an AVL tree is not the best way of doing things!");
        }

        /// <summary>
        /// Removes the given element from the tree
        /// </summary>
        /// <param name="item">The item to remove from this structure</param>
        /// <returns>If we removed the element</returns>
        public bool Remove(T item)
        {
            var visited = new Stack<Node>();
            if ( default(T) == null && item == null ) return false;
            // grab the Writer's lock.
            lock ( WriterLock )
            {
                Root = RecursiveRemove(Root, item, visited, out bool removed);
                if ( removed )
                {
                    DecreaseCount();
                    BalanceTree( visited );
                    Thread.MemoryBarrier();
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }

        /// <summary>
        /// Generates a new array that holds a snapshot of the tree, in order
        /// </summary>
        /// <returns></returns>
        public T?[] ToArray()
        {
            IncreaseReaders();
            var ret = new T?[Count];
            var i = 0;
            foreach ( var t in this )
            {
                ret[i] = t;
                i++;
            }
            DecreaseReaders();
            return ret;
        }

        /// <summary>
        /// Call this when finished adding/removing item
        /// </summary>
        /// <param name="path"></param>
        protected void BalanceTree(Stack<Node> path)
        {
            // To balance the tree we need to go back up the path and update the balances for each side
            if (path.Count == 0 || Root is null)
            {
                return;
            }
            while ( path.Count >= 2 )
            {
                BalanceAndUpdate( path.Pop(), path.Peek() );
            }
            var difference = (Root.Left?.Height ?? 0) - (Root.Right?.Height ?? 0);

            if ( difference <= -2 )
            {
                var temp = Root.Right;
                if (temp == null) throw new InvalidOperationException("A child node was null!");
                Root.Right = temp.Left;
                temp.Left = Root;
                Root = temp;
            }
            else if ( difference >= 2 )
            {
                var temp = Root.Left;
                if (temp == null) throw new InvalidOperationException("A child node was null!");
                Root.Left = temp.Right;
                temp.Right = Root;
                Root = temp;
            }
            Root.Height = Math.Max( Root.Left?.Height ?? 0, Root.Right?.Height ?? 0 ) + 1;
        }

        /// <summary>
        /// Call this instead of accessing the _Count manually
        /// </summary>
        protected void DecreaseCount()
        {
            Interlocked.Decrement( ref _Count);
        }

        /// <summary>
        /// Call this when finished reading
        /// </summary>
        protected void DecreaseReaders()
        {
            Interlocked.Decrement( ref ReaderCount);
        }

        /// <summary>
        /// Call this instead of accessing the Count manually
        /// </summary>
        protected void IncreaseCount()
        {
            // We need to atomically access _Count
            Interlocked.Increment( ref _Count);
        }

        /// <summary>
        ///
        /// </summary>
        protected void IncreaseReaders()
        {
            // This will stop writers from starting if there
            // is a remove going on.
            lock ( WriterLock )
            {
                // This is needed to make sure that we know
                // how many writers we have at any given time
                Interlocked.Increment( ref ReaderCount);
            }
        }

        /// <summary>
        /// Call this before starting to write
        /// </summary>
        protected void WaitForReaders()
        {
            // spin lock until we can write
            while (ReaderCount != 0)
            {
                Thread.Sleep(0);
            }
        }

        /// <summary>
        /// Balances the tree at the current node
        /// </summary>
        /// <param name="current"></param>
        /// <param name="parent"></param>
        private void BalanceAndUpdate(Node current, Node parent)
        {
            var difference = (current.Left?.Height ?? 0) - (current.Right?.Height ?? 0);
            if ( difference <= -2 )
            {
                RotateLeft( current, parent );
            }
            else if ( difference >= 2 )
            {
                RotateRight( current, parent );
            }
            current.Height = Math.Max( current.Left?.Height ?? 0, current.Right?.Height ?? 0 ) + 1;
        }

        /// <summary>
        /// Creates a new node and stores it at "here"
        /// </summary>
        /// <param name="item">The item we want to store</param>
        /// <param name="here">Where are are going to store it</param>
        private void MakeNode(T item, out Node here)
        {
            here = new Node
            {
                Data = item,
                Height = 0,
                Left = null,
                Right = null
            };
        }

        /// <summary>
        /// Only call this after you have gotten the writer lock
        /// </summary>
        /// <param name="current"></param>
        /// <param name="item"></param>
        /// <param name="visited">Nodes that have been visited</param>
        /// <param name="removed"></param>
        /// <returns></returns>
        private Node? RecursiveRemove(Node? current, T item, Stack<Node> visited, out bool removed)
        {
            if ( current == null )
            {
                removed = false;
                return null;
            }

            var comp = item.CompareTo( current.Data );
            if ( comp < 0 )
            {
                visited.Push( current );
                current.Left = RecursiveRemove( current.Left, item, visited, out removed );
                return current;
            }
            else if ( comp > 0 )
            {
                visited.Push( current );
                current.Right = RecursiveRemove( current.Right, item, visited, out removed );
                return current;
            }

            // We can only get here if we are at the node we want
            WaitForReaders();
            var parent = current;
            removed = true;
            // in case there are two children on this node
            if ( current.Left != null && current.Right != null )
            {
                var sack = current.Right;
                while ( sack.Left != null )
                {
                    visited.Push( parent );
                    parent = sack;
                    sack = sack.Left;
                }
                current.Data = sack.Data;
                if ( sack == current.Right )
                {
                    current.Right = sack.Right;
                }
                else
                {
                    parent.Left = sack.Right;
                }
                // we should be fine after assassinating our closest child
                return current;
            }
            else
            {
                return current.Left != null ? current.Left : current.Right;
            }
        }

        /// <summary>
        /// Rotates the tree to the left at the pivot node
        /// </summary>
        /// <param name="pivot">Where the rotation is to happen</param>
        /// <param name="parent">The pivot's parent</param>
        private void RotateLeft(Node pivot, Node parent)
        {
            var right = pivot.Right;
            pivot.Right = right!.Left;
            if ( parent.Right == pivot )
            {
                parent.Right = right;
            }
            else
            {
                parent.Left = right;
            }
            right.Left = pivot;
        }

        /// <summary>
        /// Rotates the tree to the right at the pivot node
        /// </summary>
        /// <param name="pivot">Where the rotation is to happen</param>
        /// <param name="parent">The pivot's parent</param>
        private void RotateRight(Node pivot, Node parent)
        {
            var left = pivot.Left;
            pivot.Left = left!.Right;
            if ( parent.Left == pivot )
            {
                parent.Left = left;
            }
            else
            {
                parent.Right = left;
            }
            left.Right = pivot;
        }

        /// <summary>
        /// Stores the information for each "node" of the
        /// Binary search tree
        /// </summary>
        public class Node
        {
            /// <summary>
            /// The payload
            /// </summary>
            public required T Data { get; set; }

            /// <summary>
            /// How many nodes are
            /// </summary>
            public int Height { get; set; }

            /// <summary>
            /// Nodes here are less than the _Root
            /// </summary>
            public Node? Left { get; set; }

            /// <summary>
            /// Nodes here are greater than the _Root
            /// </summary>
            public Node? Right { get; set; }
        }
    }
}