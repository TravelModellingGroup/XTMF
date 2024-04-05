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

namespace XTMF.Testing.Editing
{
    [TestClass]
    public class TestLinkedParameters
    {
        [TestMethod]
        public void TestNewLinkedParameter()
        {
            var runtime = TestXTMFCore.CreateRuntime();
            var controller = runtime.ModelSystemController;
            var msName = "TestModelSystem";
            controller.Delete( msName );
            var ms = controller.LoadOrCreate( msName );
            Assert.AreNotEqual( null, ms, "The model system 'TestModelSystem' was null!" );
            using var session = controller.EditModelSystem(ms);
            var modelSystem = session.ModelSystemModel;
            var linkedParameters = modelSystem.LinkedParameters;
            Assert.AreEqual(0, linkedParameters.Count, "The model system already had a linked parameter before we added any!");
            string? error = null;
            Assert.IsTrue(linkedParameters.NewLinkedParameter("Test", ref error), "We failed to create our first linked parameter!");
            Assert.AreEqual(1, linkedParameters.Count, "After adding a linked parameter it still reports that there isn't one linked parameter.");
            Assert.IsTrue(session.Undo(ref error));
            Assert.AreEqual(0, linkedParameters.Count, "After undoing the add new linked parameter there was still a linked parameter left!");
            Assert.IsTrue(session.Redo(ref error));
            Assert.AreEqual(1, linkedParameters.Count, "After re-adding a linked parameter it still reports that there isn't one linked parameter.");
        }

        [TestMethod]
        public void TestDeleteLinkedParameter()
        {
            var runtime = TestXTMFCore.CreateRuntime();
            var controller = runtime.ModelSystemController;
            var msName = "TestModelSystem";
            controller.Delete( msName );
            var ms = controller.LoadOrCreate( msName );
            Assert.AreNotEqual( null, ms, "The model system 'TestModelSystem' was null!" );
            using var session = controller.EditModelSystem(ms);
            var modelSystem = session.ModelSystemModel;
            var linkedParameters = modelSystem.LinkedParameters;
            Assert.AreEqual(0, linkedParameters.Count, "The model system already had a linked parameter before we added any!");
            string? error = null;
            Assert.IsTrue(linkedParameters.NewLinkedParameter("Test", ref error), "We failed to create our first linked parameter!");
            Assert.AreEqual(1, linkedParameters.Count, "After adding a linked parameter it still reports that there isn't one linked parameter.");
            var linkedParameterList = linkedParameters.GetLinkedParameters();
            var zeroElement = linkedParameterList[0];
            Assert.IsTrue(linkedParameters.RemoveLinkedParameter(zeroElement, ref error), "We were unable to delete the linked parameter!");
            Assert.IsFalse(linkedParameters.RemoveLinkedParameter(zeroElement, ref error), "We were able to delete the linked parameter for a second time!");
            Assert.AreEqual(0, linkedParameters.Count, "A linked parameter remained after deleting the only one!");
            Assert.IsTrue(session.Undo(ref error));
            Assert.AreEqual(1, linkedParameters.Count, "After undoing the linked parameter was not added back!");
            Assert.IsTrue(session.Redo(ref error));
            Assert.AreEqual(0, linkedParameters.Count, "After redoing the linked parameter remained!");
        }
    }
}
