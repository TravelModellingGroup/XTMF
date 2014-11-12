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

namespace XTMF.Networking
{
    internal class RemoteXTMF : IRemoteXTMF, IDisposable
    {
        /// <summary>
        /// The message queue that we are going to be using
        /// </summary>
        internal MessageQueue<Message> Messages = new MessageQueue<Message>();

        public RemoteXTMF()
        {
            this.Connected = true;
        }

        ~RemoteXTMF()
        {
            this.Dispose( true );
        }

        public bool Connected { get; set; }

        public string MachineName
        {
            get;
            internal set;
        }

        public float Progress
        {
            get;
            internal set;
        }

        public string UniqueID
        {
            get;
            internal set;
        }

        public void Dispose()
        {
            this.Dispose( false );
        }

        public void PollProgress()
        {
            this.Messages.Add( new Message( MessageType.RequestProgress ) );
        }

        public void SendCancel(string reason)
        {
            this.Messages.Add( new Message( MessageType.PostCancel, reason ) );
        }

        public void SendCustomMessage(object data, int customMessageNumber)
        {
            this.Messages.Add( new Message( MessageType.SendCustomMessage,
                new SendCustomMessageMessage() { CustomMessageNumber = customMessageNumber, Data = data } ) );
        }

        public void SendModelSystem(IModelSystemStructure structure)
        {
            this.Messages.Add( new Message( MessageType.SendModelSystem, structure ) );
        }

        protected void Dispose(bool gcCall)
        {
            if ( !gcCall )
            {
                GC.SuppressFinalize( this );
            }
            if ( this.Messages != null )
            {
                this.Messages.Dispose();
                this.Messages = null;
            }
        }
    }
}