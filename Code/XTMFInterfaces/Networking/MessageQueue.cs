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
using System.Collections.Concurrent;
using System.Threading;

namespace XTMF.Networking
{
    /// <summary>
    /// Provides a clean way of waiting for
    /// data to arrive before processing it.
    /// If not ready, then the  thread will sleep.
    /// When it is ready, it will wake up.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class MessageQueue<T> : IDisposable
    {
        private ConcurrentQueue<T> Messages = new ConcurrentQueue<T>();
        private SemaphoreSlim Sem = new SemaphoreSlim( 0 );

        /// <summary>
        /// Add a new message to the queue
        /// </summary>
        /// <param name="message">The message to be added</param>
        public void Add(T message)
        {
            // we need to enqueue it before we add an extra count
            Messages.Enqueue( message );
            Sem.Release();
        }

        public void Dispose()
        {
            Dispose( true );
        }

        /// <summary>
        /// Retrieve a message from the MessageQueue
        /// This will wait indefinitely for the next message
        /// </summary>
        /// <returns>The next message</returns>
        public T GetMessage()
        {
            Sem.Wait();
            T ret;
            // this should always succeed
            if ( !Messages.TryDequeue( out ret ) )
            {
                return default(T);
            }
            return ret;
        }

        /// <summary>
        /// Retrieve a message from the Messagequeue.  This will wait for a given amount of time.
        /// </summary>
        /// <param name="timeout">The length of time to wait at most in milliseconds before returning</param>
        /// <returns>The next message, if the timeout occurs the default value</returns>
        public T GetMessageOrTimeout(int timeout)
        {
            if ( Sem.Wait( timeout ) )
            {
                T ret;
                if ( !Messages.TryDequeue( out ret ) )
                {
                    return default(T);
                }
                return ret;
            }
            return default(T);
        }

        /// <summary>
        /// Gets a peak at the current number of messages pending.
        /// </summary>
        public int Count
        {
            get
            {
                return Messages.Count;
            }
        }

        protected virtual void Dispose(bool includeManaged)
        {
            Sem.Dispose();
            Sem = null;
        }
    }
}