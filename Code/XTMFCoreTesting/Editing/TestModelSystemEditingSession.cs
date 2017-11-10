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

using System.Linq;
using XTMF.Testing.Modules;
using XTMF.Testing.Modules.Editing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading;
using System.Collections.Generic;
using System;

namespace XTMF.Testing.Editing
{
    [TestClass]
    public class TestModelSystemEditingSession
    {
        [TestMethod]
        public void TestCreatingEditSession()
        {
            var runtime = TestXTMFCore.CreateRuntime();
            var controller = runtime.ModelSystemController;
            var msName = "TestModelSystem";
            controller.Delete(msName);
            var ms = controller.LoadOrCreate(msName);
            Assert.AreNotEqual(null, ms, "The model system 'TestModelSystem' was null!");
            var session = controller.EditModelSystem(ms);
            try
            {
                var secondSession = controller.EditModelSystem(ms);
                try
                {
                    using (session)
                    {
                        Assert.IsTrue(session.EditingModelSystem, "The session doesn't believe that it is editing a model system!");
                        Assert.IsFalse(session.EditingProject, "The session believes that is it editing a project!");
                        Assert.IsTrue(session == secondSession, "The two given sessions for the same model system are not the same!");
                    }
                    session = null;
                    var thirdSession = controller.EditModelSystem(ms);
                    using (thirdSession)
                    {
                        Assert.IsTrue(secondSession == thirdSession, "The second session and the third session should have been the same!");
                        secondSession.Dispose();
                    }
                    secondSession = null;
                    using (var fourthSession = controller.EditModelSystem(ms))
                    {
                        Assert.IsFalse(thirdSession == fourthSession, "The third session and the fourth session should have been different!");
                    }
                }
                finally
                {
                    if(secondSession != null)
                    {
                        secondSession.Dispose();
                    }
                }
            }
            finally
            {
                if(session != null)
                {
                    session.Dispose();
                }
            }
        }

        [TestMethod]
        public void TestEditingModelSystem()
        {
            var runtime = TestXTMFCore.CreateRuntime();
            var controller = runtime.ModelSystemController;
            var msName = "TestModelSystem";
            controller.Delete(msName);
            var ms = controller.LoadOrCreate(msName);
            Assert.AreNotEqual(null, ms, "The model system 'TestModelSystem' was null!");
            using (var session = controller.EditModelSystem(ms))
            {
                var model = session.ModelSystemModel;
                Assert.IsNotNull(model, "No model system model was created!");
                ModelSystemStructureModel root = model.Root;
                Assert.IsNotNull(root, "No root object was made!");
            }
        }

        [TestMethod]
        public void TestUndoRedo()
        {
            var runtime = TestXTMFCore.CreateRuntime();
            var controller = runtime.ModelSystemController;
            var msName = "TestModelSystem";
            controller.Delete(msName);
            var ms = controller.LoadOrCreate(msName);
            Assert.AreNotEqual(null, ms, "The model system 'TestModelSystem' was null!");
            using (var session = controller.EditModelSystem(ms))
            {
                var model = session.ModelSystemModel;
                Assert.IsNotNull(model, "No model system model was created!");
                ModelSystemStructureModel root = model.Root;
                Assert.IsNotNull(root, "No root object was made!");

                root.Type = typeof(TestModelSystemTemplate);
                Assert.AreEqual(typeof(TestModelSystemTemplate), root.Type, "The root was not updated to the proper type!");
                string error = null;
                Assert.IsTrue(session.Undo(ref error), "The undo failed!");
                Assert.AreEqual(null, root.Type, "The root was not updated to the proper type after undo!");

                Assert.IsTrue(session.Redo(ref error), "The undo failed!");
                Assert.AreEqual(typeof(TestModelSystemTemplate), root.Type, "The root was not updated to the proper type after redo!");
            }
        }

        [TestMethod]
        public void TestUndoAndChildrenSize()
        {
            var runtime = TestXTMFCore.CreateRuntime();
            var controller = runtime.ModelSystemController;
            var msName = "TestModelSystem";
            controller.Delete(msName);
            var ms = controller.LoadOrCreate(msName);
            Assert.AreNotEqual(null, ms, "The model system 'TestModelSystem' was null!");
            using (var session = controller.EditModelSystem(ms))
            {
                var model = session.ModelSystemModel;
                Assert.IsNotNull(model, "No model system model was created!");
                ModelSystemStructureModel root = model.Root;
                Assert.IsNotNull(root, "No root object was made!");

                root.Type = typeof(TestModelSystemTemplate);
                Assert.AreEqual(typeof(TestModelSystemTemplate), root.Type, "The root was not updated to the proper type!");
                Assert.AreEqual(1, root.Children.Count);
                string error = null;
                Assert.IsTrue(session.Undo(ref error), "The undo failed!");
                Assert.AreEqual(null, root.Type, "The root was not updated to the proper type after undo!");
                Assert.AreEqual(0, root.Children.Count, "There should be no children!");

                Assert.IsTrue(session.Redo(ref error), "The undo failed!");
                Assert.AreEqual(typeof(TestModelSystemTemplate), root.Type, "The root was not updated to the proper type after redo!");
            }
        }

        [TestMethod]
        public void TestAddingACollectionMember()
        {
            var runtime = TestXTMFCore.CreateRuntime();
            var controller = runtime.ModelSystemController;
            var msName = "TestModelSystem";
            controller.Delete(msName);
            var ms = controller.LoadOrCreate(msName);
            Assert.AreNotEqual(null, ms, "The model system 'TestModelSystem' was null!");
            using (var session = controller.EditModelSystem(ms))
            {
                var model = session.ModelSystemModel;
                Assert.IsNotNull(model, "No model system model was created!");
                ModelSystemStructureModel root = model.Root;
                Assert.IsNotNull(root, "No root object was made!");

                root.Type = typeof(TestModelSystemTemplate);
                Assert.AreEqual(typeof(TestModelSystemTemplate), root.Type, "The root was not updated to the proper type!");

                Assert.IsNotNull(root.Children, "The test model system template doesn't have any children models!");

                var collection = root.Children.FirstOrDefault((child) => child.Name == "Test Collection");
                Assert.IsNotNull(collection, "We were unable to find a child member that contained the test collection!");

                string error = null;
                Assert.IsTrue(collection.AddCollectionMember(typeof(TestModule), ref error), "We were unable to properly add a new collection member.");
                Assert.IsFalse(collection.AddCollectionMember(typeof(int), ref error), "We were able to use an integer as a collection member!");
            }
        }

        [TestMethod]
        public void TestRemovingCollectionMember()
        {
            var runtime = TestXTMFCore.CreateRuntime();
            var controller = runtime.ModelSystemController;
            var msName = "TestModelSystem";
            controller.Delete(msName);
            var ms = controller.LoadOrCreate(msName);
            Assert.AreNotEqual(null, ms, "The model system 'TestModelSystem' was null!");
            using (var session = controller.EditModelSystem(ms))
            {
                var model = session.ModelSystemModel;
                Assert.IsNotNull(model, "No model system model was created!");
                ModelSystemStructureModel root = model.Root;
                Assert.IsNotNull(root, "No root object was made!");

                root.Type = typeof(TestModelSystemTemplate);
                Assert.AreEqual(typeof(TestModelSystemTemplate), root.Type, "The root was not updated to the proper type!");

                Assert.IsNotNull(root.Children, "The test model system template doesn't have any children models!");

                var collection = root.Children.FirstOrDefault((child) => child.Name == "Test Collection");
                Assert.IsNotNull(collection, "We were unable to find a child member that contained the test collection!");

                string error = null;
                Assert.IsTrue(collection.AddCollectionMember(typeof(TestModule), ref error), "We were unable to properly add a new collection member.");
                Assert.IsFalse(collection.AddCollectionMember(typeof(int), ref error), "We were able to use an integer as a collection member!");

                Assert.IsFalse(collection.RemoveCollectionMember(1, ref error), "We received a success while trying to remove a collection member that doesn't exist!");
                Assert.IsTrue(collection.RemoveCollectionMember(0, ref error), "We were unable to remove a collection member that does exist!");
                Assert.AreEqual(0, collection.Children.Count);
                Assert.IsTrue(session.Undo(ref error), "We were unable to undo the remove collection!");
                Assert.AreEqual(1, collection.Children.Count, "The undo did not add back our collection member!");
            }
        }

        [TestMethod]
        public void TestRemovingEntireCollection()
        {
            var runtime = TestXTMFCore.CreateRuntime();
            var controller = runtime.ModelSystemController;
            var msName = "TestModelSystem";
            controller.Delete(msName);
            var ms = controller.LoadOrCreate(msName);
            Assert.AreNotEqual(null, ms, "The model system 'TestModelSystem' was null!");
            using (var session = controller.EditModelSystem(ms))
            {
                var model = session.ModelSystemModel;
                Assert.IsNotNull(model, "No model system model was created!");
                ModelSystemStructureModel root = model.Root;
                Assert.IsNotNull(root, "No root object was made!");

                root.Type = typeof(TestModelSystemTemplate);
                Assert.AreEqual(typeof(TestModelSystemTemplate), root.Type, "The root was not updated to the proper type!");

                Assert.IsNotNull(root.Children, "The test model system template doesn't have any children models!");

                var collection = root.Children.FirstOrDefault((child) => child.Name == "Test Collection");
                Assert.IsNotNull(collection, "We were unable to find a child member that contained the test collection!");

                string error = null;
                Assert.IsTrue(collection.AddCollectionMember(typeof(TestModule), ref error), "We were unable to properly add a new collection member.");
                Assert.IsTrue(collection.AddCollectionMember(typeof(TestModule), ref error), "We were unable to properly add a new collection member.");
                Assert.IsFalse(collection.AddCollectionMember(typeof(int), ref error), "We were able to use an integer as a collection member!");

                Assert.AreEqual(2, collection.Children.Count, "An incorrect number of children were found.");
                var oldChildren = collection.Children.ToList();
                Assert.IsTrue(collection.RemoveAllCollectionMembers(ref error), "We were unable to remove all collection members!");
                Assert.AreEqual(0, collection.Children.Count, "After removing all of the collection members, there were still elements left in the collection.");
                Assert.IsTrue(session.Undo(ref error), "We failed to undo the remove all!");
                Assert.AreEqual(2, collection.Children.Count, "After undoing the remove all there were still issues.");

                for(int i = 0; i < collection.Children.Count; i++)
                {
                    Assert.AreEqual(oldChildren[i], collection.Children[i], "A child was not the same as before!");
                }
            }
        }


        [TestMethod]
        public void TestMovingCollectionMember()
        {
            var runtime = TestXTMFCore.CreateRuntime();
            var controller = runtime.ModelSystemController;
            var msName = "TestModelSystem";
            controller.Delete(msName);
            var ms = controller.LoadOrCreate(msName);
            Assert.AreNotEqual(null, ms, "The model system 'TestModelSystem' was null!");
            using (var session = controller.EditModelSystem(ms))
            {
                var model = session.ModelSystemModel;
                Assert.IsNotNull(model, "No model system model was created!");
                ModelSystemStructureModel root = model.Root;
                Assert.IsNotNull(root, "No root object was made!");

                root.Type = typeof(TestModelSystemTemplate);
                Assert.AreEqual(typeof(TestModelSystemTemplate), root.Type, "The root was not updated to the proper type!");

                Assert.IsNotNull(root.Children, "The test model system template doesn't have any children models!");

                var collection = root.Children.FirstOrDefault((child) => child.Name == "Test Collection");
                Assert.IsNotNull(collection, "We were unable to find a child member that contained the test collection!");

                string error = null;
                Assert.IsTrue(collection.AddCollectionMember(typeof(TestModule), ref error), "We were unable to properly add a new collection member.");
                Assert.IsTrue(collection.AddCollectionMember(typeof(TestModule), ref error), "We were unable to properly add a new collection member.");
                var members = collection.Children;
                var first = members[0];
                var second = members[1];
                Assert.IsFalse(collection.MoveChild(-1, 0, ref error));
                Assert.IsFalse(collection.MoveChild(0, -1, ref error));
                Assert.IsFalse(collection.MoveChild(2, 0, ref error));
                Assert.IsFalse(collection.MoveChild(0, 2, ref error));
                Assert.IsTrue(collection.MoveChild(0, 1, ref error));
                Assert.AreEqual(first, collection.Children[1]);
                Assert.AreEqual(second, collection.Children[0]);

            }
        }

        [TestMethod]
        public void TestChangingAModulesName()
        {
            var runtime = TestXTMFCore.CreateRuntime();
            var controller = runtime.ModelSystemController;
            var msName = "TestModelSystem";
            controller.Delete(msName);
            var ms = controller.LoadOrCreate(msName);
            Assert.AreNotEqual(null, ms, "The model system 'TestModelSystem' was null!");
            using (var session = controller.EditModelSystem(ms))
            {
                string error = null;
                var model = session.ModelSystemModel;
                Assert.IsNotNull(model, "No model system model was created!");
                ModelSystemStructureModel root = model.Root;
                Assert.IsNotNull(root, "No root object was made!");
                var oldName = root.Name;
                const string newName = "New Name";
                Assert.IsTrue(root.SetName(newName, ref error), "Failed to set the module's name!");
                Assert.AreEqual(root.Name, newName, "The new name was not assigned!");
                if(!session.Undo(ref error))
                {
                    Assert.Fail("We were unable to undo! " + error);
                }
                Assert.AreEqual(root.Name, oldName, "The old name was not restored!");
                if(!session.Redo(ref error))
                {
                    Assert.Fail("We were unable to redo! " + error);
                }
                Assert.AreEqual(root.Name, newName, "The new name was not restored after redo!");
            }
        }

        [TestMethod]
        public void TestRun()
        {
            var runtime = TestXTMFCore.CreateRuntime();
            var controller = runtime.ProjectController;
            string error = null;
            controller.DeleteProject("TestProject", ref error);
            Project project;
            Assert.IsTrue((project = controller.LoadOrCreate("TestProject", ref error)) != null);
            ((Configuration)runtime.Configuration).RunInSeperateProcess = false;
            using (var session = controller.EditProject(project))
            {
                var testModelSystem = CreateTestModelSystem(runtime);
                Assert.IsTrue(session.AddModelSystem(testModelSystem, ref error));
                using (var modelSystemSession = session.EditModelSystem(0))
                {
                    Assert.IsNotNull(modelSystemSession);
                    var root = modelSystemSession.ModelSystemModel.Root;
                    var collection = root.Children.FirstOrDefault((child) => child.Name == "Test Collection");
                    Assert.IsNotNull(collection);
                    XTMFRun run;
                    Assert.IsNotNull(run = modelSystemSession.Run("TestRun", ref error));
                    bool finished = false;
                    List<ErrorWithPath> errors = null;
                    Action<List<ErrorWithPath>> catchErrors = (errorPath) =>
                    {
                        errors = errorPath;
                        finished = true;
                    };
                    run.ValidationError += catchErrors;
                    run.RuntimeValidationError += catchErrors;
                    run.RunCompleted += () =>
                    {
                        finished = true;
                    };
                    Assert.IsFalse(collection.AddCollectionMember(typeof(TestModule), ref error));
                    run.Start();
                    for(int i = 0; i < 10000 & !finished; i++)
                    {
                        Thread.Sleep(i);
                        Thread.MemoryBarrier();
                    }
                    if(!finished)
                    {
                        Assert.Fail("The model system did not complete in time.");
                    }
                    if(errors != null)
                    {
                        Assert.Fail(errors[0].Message);
                    }
                    // now that it is done we should be able to edit it again
                    Assert.IsTrue(collection.AddCollectionMember(typeof(TestModule), ref error), error);
                }
            }
        }

        private ModelSystem CreateTestModelSystem(XTMFRuntime runtime)
        {
            var controller = runtime.ModelSystemController;
            controller.Delete("TestModelSystem");
            var modelSystem = controller.LoadOrCreate("TestModelSystem");
            string error = null;
            using (var session = controller.EditModelSystem(modelSystem))
            {
                var root = session.ModelSystemModel.Root;
                root.Type = typeof(TestModelSystemTemplate);
                Assert.IsTrue(session.Save(ref error));
            }
            return modelSystem;
        }

    }
}
