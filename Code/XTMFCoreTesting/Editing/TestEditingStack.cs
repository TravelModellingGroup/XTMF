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

using Microsoft.VisualStudio.TestTools.UnitTesting;
using XTMF.Editing;
namespace XTMF.Testing
{
    [TestClass]
    public class TestEditingStack
    {
        private class TestCommand : XTMFCommand
        {
            public TestCommand(string name = "") : base(name)
            {

            }

            public override bool CanUndo()
            {
                return false;
            }

            public override bool Do(ref string error)
            {
                return true;
            }

            public override bool Redo(ref string error)
            {
                return true;
            }

            public override bool Undo(ref string error)
            {
                return true;
            }
        }

        [TestMethod]
        public void TestEditingStackOperations()
        {
            var commands = new XTMFCommand[20];
            for ( int i = 0; i < commands.Length; i++ )
            {
                commands[i] = new TestCommand();
            }
            EditingStack stack = new EditingStack( 10 );
            Assert.AreEqual( 0, stack.Count, "The stack's count is incorrect!" );
            // fill the stack
            for ( int i = 0; i < 10; i++ )
            {
                stack.Add( commands[i] );
                Assert.AreEqual( i + 1, stack.Count, "The stack's count is incorrect!" );
            }
            // over fill the stack
            for ( int i = 10; i < 20; i++ )
            {
                stack.Add( commands[i] );
                Assert.AreEqual( 10, stack.Count, "The stack's count is incorrect!" );
            }
            // Make sure the first don't exist anymore
            for ( int i = 0; i < 10; i++ )
            {
                Assert.AreEqual( false, stack.Contains( commands[i] ), "The stack retained a command it should have lost!" );
            }
            // Make sure the newer ones still exist
            for ( int i = 10; i < 20; i++ )
            {
                Assert.AreEqual( true, stack.Contains( commands[i] ), "The stack lost a command it should have retained!" );
            }
            for (int i = 19; i >= 10; i--)
            {
                if (stack.TryPop(out XTMFCommand command))
                {
                    Assert.AreEqual(commands[i], command, "While popping we popped an unexpected command!");
                }
                else
                {
                    Assert.Fail("A pop failed that should have succeeded!");
                }
            }
        }
    }
}
