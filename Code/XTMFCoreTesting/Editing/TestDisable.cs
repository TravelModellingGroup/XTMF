/*
    Copyright 2017 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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

using XTMF.Testing.Modules.Editing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using XTMF.Testing.Modules;

namespace XTMF.Testing.Editing
{
    [TestClass]
    public class TestDisable
    {
        [TestMethod]
        public void TestDisabling()
        {
            var runtime = TestXTMFCore.CreateRuntime();
            var controller = runtime.ModelSystemController;
            var msName = "TestModelSystem";
            controller.Delete(msName);
            var ms = controller.LoadOrCreate(msName);
            Assert.AreNotEqual(null, ms, "The model system 'TestModelSystem' was null!");
            using var session = controller.EditModelSystem(ms);
            string? error = null;
            // build a small model system
            var model = session.ModelSystemModel;
            Assert.IsNotNull(model, "No model system model was created!");
            ModelSystemStructureModel root = model.Root;
            Assert.IsNotNull(root, "No root object was made!");
            root.Type = typeof(TestModelSystemTemplate);
            // disable a module
            Assert.IsTrue(root.Children[0].AddCollectionMember(typeof(TestModule), ref error, "SimpleChild"), error);
            var simpleChild = root.Children[0].Children[0];
            Assert.AreEqual(false, simpleChild.IsDisabled, "By default simple child should not be disabled!");
            Assert.IsTrue(simpleChild.SetDisabled(true, ref error), error);
            Assert.AreEqual(true, simpleChild.IsDisabled, "By default simple child should have been disabled!");
            Assert.IsTrue(session.Undo(ref error), error);
            Assert.AreEqual(false, simpleChild.IsDisabled, "By default simple child should have been re-enabled!");
        }

        [TestMethod]
        public void TestDisablingRequiredField()
        {
            var runtime = TestXTMFCore.CreateRuntime();
            var controller = runtime.ModelSystemController;
            var msName = "TestModelSystem";
            controller.Delete(msName);
            var ms = controller.LoadOrCreate(msName);
            Assert.AreNotEqual(null, ms, "The model system 'TestModelSystem' was null!");
            using var session = controller.EditModelSystem(ms);
            string? error = null;
            // build a small model system
            var model = session.ModelSystemModel;
            Assert.IsNotNull(model, "No model system model was created!");
            ModelSystemStructureModel root = model.Root;
            Assert.IsNotNull(root, "No root object was made!");
            root.Type = typeof(TestModelSystemTemplate);
            // disable a module
            Assert.IsTrue(root.Children[0].AddCollectionMember(typeof(TestRequiredSubmodule), ref error, "SimpleChild"), error);
            var requiredParent = root.Children[0].Children[0];
            requiredParent.Children[0].Type = typeof(TestModule);
            var simpleChild = requiredParent.Children[0];
            Assert.AreEqual(false, simpleChild.IsDisabled, "By default simple child should not be disabled!");
            Assert.IsFalse(simpleChild.SetDisabled(true, ref error), "You should not be able to disable a required submodule!");
        }

        [TestMethod]
        public void TestDisableLoadSave()
        {
            var runtime = TestXTMFCore.CreateRuntime();
            var controller = runtime.ModelSystemController;
            var msName = "TestModelSystem";
            controller.Delete(msName);
            var ms = controller.LoadOrCreate(msName);
            Assert.AreNotEqual(null, ms, "The model system 'TestModelSystem' was null!");
            using (var session = controller.EditModelSystem(ms))
            {
                string? error = null;
                // build a small model system
                var model = session.ModelSystemModel;
                Assert.IsNotNull(model, "No model system model was created!");
                ModelSystemStructureModel root = model.Root;
                Assert.IsNotNull(root, "No root object was made!");
                root.Type = typeof(TestModelSystemTemplate);
                // disable a module
                Assert.IsTrue(root.Children[0].AddCollectionMember(typeof(TestModule), ref error, "SimpleChild"), error);
                var simpleChild = root.Children[0].Children[0];
                Assert.IsTrue(root.Children[0].SetDisabled(true, ref error), error);
                Assert.AreEqual(false, simpleChild.IsDisabled, "By default simple child should not be disabled!");
                Assert.IsTrue(simpleChild.SetDisabled(true, ref error), error);
                Assert.AreEqual(true, simpleChild.IsDisabled, "By default simple child should have been disabled!");
                Assert.IsTrue(session.Save(ref error), error);
            }
            // now reload in the data
            ms = controller.LoadOrCreate(msName);
            Assert.AreNotEqual(null, ms, "The model system 'TestModelSystem' was null!");
            using (var session = controller.EditModelSystem(ms))
            {
                var root = session.ModelSystemModel.Root;
                Assert.IsFalse(root.IsDisabled, "The root should not be disabled!");
                var simpleChild = root.Children[0].Children[0];
                Assert.IsTrue(simpleChild.IsDisabled, "The simple child that was loaded was not disabled!");
            }
        }
    }
}
