using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Datastructure
{
    public sealed class RTree2D<T> : ICollection<T>
    {
        private int ChildLength;
        private Node Root;
        public RTree2D(int m = 10)
        {
            this.ChildLength = m;
        }

        private struct BoundingRect
        {
            private double X, Y, X2, Y2;

            public BoundingRect(double x, double y, double x2, double y2)
            {
                if ( x > x2 )
                {
                    double temp = x;
                    x = x2;
                    x2 = temp;
                }
                if ( y > y2 )
                {
                    double temp = y;
                    y = y2;
                    y2 = temp;
                }
                this.X = x;
                this.Y = y;
                this.X2 = x2;
                this.Y2 = y2;
            }

            internal bool Contains(BoundingRect rect)
            {
                return ( this.X > rect.X ?
                    rect.X2 <= this.X : this.X2 < rect.X ) &
                    ( this.Y > rect.Y ?
                    rect.Y2 <= this.Y : this.Y2 < rect.Y );
            }

            internal BoundingRect Union(BoundingRect rect)
            {
                return Union( this, rect );
            }

            internal static BoundingRect Union(BoundingRect first, BoundingRect second)
            {
                BoundingRect rect;
                rect.X = first.X < second.X ? first.X : second.X;
                rect.Y = first.Y < second.Y ? first.Y : second.Y;
                rect.X2 = first.X2 > second.X2 ? first.X2 : second.X2;
                rect.Y2 = first.Y2 > second.Y2 ? first.Y2 : second.Y2;
                return rect;
            }
        }

        private class Node
        {
            internal BoundingRect BoundingBox;
            internal Node[] Children;
            internal T Data;
            internal int Depth;

            internal bool Contains(BoundingRect rect)
            {
                return this.BoundingBox.Contains( rect );
            }

            internal void Balance()
            {
                throw new NotImplementedException();
            }
        }

        public void Add(double x, double y, double x2, double y2, T data)
        {
            BoundingRect rect = new BoundingRect( x, y, x2, y2 );
            if ( this.Root == null )
            {
                this.Root = new Node()
                {
                    BoundingBox = rect,
                    Children = new Node[this.ChildLength],
                    Data = data
                };
            }
            else
            {
                Add( this.Root, rect, data );
            }
        }

        private void Add(Node currentNode, BoundingRect rect, T data)
        {
            var children = currentNode.Children;
            int emptyPosition = -1;
            for ( int i = 0; i < children.Length; i++ )
            {
                if ( children[i] == null )
                {
                    emptyPosition = i;
                    break;
                }
                if ( children[i].Contains( rect ) )
                {
                    this.Add( children[i], rect, data );
                    currentNode.Depth = Math.Max( children[i].Depth, currentNode.Depth );
                    currentNode.Balance();
                    return;
                }
            }
            // if no child already contains that point
            // We need to expand to contain it
            currentNode.BoundingBox = BoundingRect.Union( currentNode.BoundingBox, rect );
            // check to see if there is a place we can just store it quickly
            if ( emptyPosition >= 0 )
            {
                children[emptyPosition] = new Node() { Depth = 0, BoundingBox = rect, Children = new Node[this.ChildLength], Data = data };
                currentNode.Depth = Math.Max( currentNode.Depth, 1 );
            }
            else
            {
                // if there is no place to store it we need to force a merge into a child that does not contain it
            }
            currentNode.Balance();
        }
    }
}
