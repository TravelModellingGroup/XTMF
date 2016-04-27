/*
    Copyright 2016 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using XTMF.Testing.Modules;
using XTMF.Testing.Modules.Editing;

namespace XTMF.Testing.Editing
{
    [TestClass]
    public class TestGenericModule
    {

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
                if(!root.Children[0].AddCollectionMember(typeof(TestGenericModule<float,float>), ref error))
                {
                    Assert.Fail(error);
                }
                Assert.IsTrue(session.Save(ref error));
            }
            return modelSystem;
        }

        private ModelSystem CreateSpecificTestModelSystem(XTMFRuntime runtime)
        {
            var controller = runtime.ModelSystemController;
            controller.Delete("TestModelSystem");
            var modelSystem = controller.LoadOrCreate("TestModelSystem");
            string error = null;
            using (var session = controller.EditModelSystem(modelSystem))
            {
                var root = session.ModelSystemModel.Root;
                root.Type = typeof(TestSpecificGenericModuleMST);
                Assert.IsTrue(session.Save(ref error));
            }
            return modelSystem;
        }

        [TestMethod]
        public void TestAddingAGenericModule()
        {
            var runtime = TestXTMFCore.CreateRuntime();
            var controller = runtime.ProjectController;
            string error = null;
            controller.DeleteProject("TestProject", ref error);
            Project project;
            Assert.IsTrue((project = controller.LoadOrCreate("TestProject", ref error)) != null);
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
                    run.RunComplete += () =>
                    {
                        finished = true;
                    };
                    Assert.IsFalse(collection.AddCollectionMember(typeof(TestModule), ref error));
                    run.Start();
                    for (int i = 0; i < 100 & !finished; i++)
                    {
                        Thread.Sleep(i);
                        Thread.MemoryBarrier();
                    }
                    if (!finished)
                    {
                        Assert.Fail("The model system did not complete in time.");
                    }
                    // now that it is done we should be able to edit it again
                    Assert.IsTrue(collection.AddCollectionMember(typeof(TestModule), ref error), error);
                }
            }
        }

        [TestMethod]
        public void TestGettingGenericType()
        {
            var runtime = TestXTMFCore.CreateRuntime();
            var projectController = runtime.ProjectController;
            var msController = runtime.ModelSystemController;
            string error = null;
            projectController.DeleteProject("TestProject", ref error);
            Project project;
            Assert.IsTrue((project = projectController.LoadOrCreate("TestProject", ref error)) != null);
            using (var session = projectController.EditProject(project))
            {
                var testModelSystem = CreateTestModelSystem(runtime);
                Assert.IsTrue(session.AddModelSystem(testModelSystem, ref error));
                using (var modelSystemSession = session.EditModelSystem(0))
                {
                    Assert.IsNotNull(modelSystemSession);
                    var root = modelSystemSession.ModelSystemModel.Root;
                    var collection = root.Children.FirstOrDefault((child) => child.Name == "Test Collection");
                    Assert.IsNotNull(collection);
                    var availableModules = modelSystemSession.GetValidModules(collection);
                    if(!availableModules.Any(m => m.Name == "TestGenericModule`2" && m.ContainsGenericParameters))
                    {
                        Assert.Fail();
                    }
                }
            }
        }

        [TestMethod]
        public void TestGettingSpecificGenericType()
        {
            var runtime = TestXTMFCore.CreateRuntime();
            var projectController = runtime.ProjectController;
            var msController = runtime.ModelSystemController;
            string error = null;
            projectController.DeleteProject("TestProject", ref error);
            Project project;
            Assert.IsTrue((project = projectController.LoadOrCreate("TestProject", ref error)) != null);
            using (var session = projectController.EditProject(project))
            {
                var testModelSystem = CreateSpecificTestModelSystem(runtime);
                Assert.IsTrue(session.AddModelSystem(testModelSystem, ref error));
                using (var modelSystemSession = session.EditModelSystem(0))
                {
                    Assert.IsNotNull(modelSystemSession);
                    var root = modelSystemSession.ModelSystemModel.Root;
                    var collection = root.Children.FirstOrDefault((child) => child.Name == "My Child");
                    Assert.IsNotNull(collection);
                    var availableModules = modelSystemSession.GetValidModules(collection);
                    if (!availableModules.Any(m => m.Name == "TestGenericModule`2" ))
                    {
                        Assert.Fail();
                    }
                }
            }
        }
    }
}
