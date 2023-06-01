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

using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using XTMF.Testing.Modules.Editing;

namespace XTMF.Testing.Editing
{
    [TestClass]
    public class TestImportingExportingModelSystem
    {
        [TestMethod]
        public void TestExportingModelSystemToString()
        {
            var runtime = TestXTMFCore.CreateRuntime();
            var controller = runtime.ProjectController;
            string? error = null;
            controller.DeleteProject("TestProject", ref error);
            Project project;
            Assert.IsTrue((project = controller.LoadOrCreate("TestProject", ref error)) != null);
            using (var session = controller.EditProject(project))
            {
                var testModelSystem = CreateTestModelSystem(runtime);
                Assert.IsTrue(session.AddModelSystem(testModelSystem, ref error));
                Assert.IsTrue(session.ExportModelSystemAsString(0, out string modelSystem, ref error), error);
            }
        }

        [TestMethod]
        public void TestImportingModelSystemFromString()
        {
            var runtime = TestXTMFCore.CreateRuntime();
            var controller = runtime.ProjectController;
            string? error = null;
            controller.DeleteProject("TestProject", ref error);
            Project project;
            Assert.IsTrue((project = controller.LoadOrCreate("TestProject", ref error)) != null);
            using (var session = controller.EditProject(project))
            {
                var testModelSystem = CreateTestModelSystem(runtime);
                Assert.IsTrue(session.AddModelSystem(testModelSystem, ref error));
                Assert.IsTrue(session.ExportModelSystemAsString(0, out string modelSystem, ref error), error);
                Assert.IsTrue(session.ImportModelSystemFromString(modelSystem, "TestModelSystem2", ref error), error);
                Assert.AreEqual(2, session.Project.ModelSystemStructure.Count);
                Assert.AreEqual("TestModelSystem2", session.Project.ModelSystemStructure[1].Name);
            }
        }

        [TestMethod]
        public void TestImportModelSystemFromFileIntoProject()
        {
            var runtime = TestXTMFCore.CreateRuntime();
            var controller = runtime.ProjectController;
            string? error = null;
            controller.DeleteProject("TestProject", ref error);
            Project project;
            Assert.IsTrue((project = controller.LoadOrCreate("TestProject", ref error)) != null);
            using (var session = controller.EditProject(project))
            {
                var testModelSystem = CreateTestModelSystem(runtime);
                Assert.IsTrue(session.AddModelSystem(testModelSystem, ref error));
                var modelSystemDirectory = session.GetConfiguration().ModelSystemDirectory;
                Assert.IsTrue(!String.IsNullOrWhiteSpace(modelSystemDirectory));
                var modelSystemFileName = Path.Combine(modelSystemDirectory, "TestModelSystem.xml");
                var fileInfo = new FileInfo(modelSystemFileName);
                Assert.IsTrue(fileInfo.Exists);
                Assert.AreEqual(1, session.Project.ModelSystemStructure.Count);
                Assert.IsTrue(session.ImportModelSystemFromFile(modelSystemFileName, "TestModelSystem2", ref error), error);
                Assert.AreEqual(2, session.Project.ModelSystemStructure.Count);
                Assert.AreEqual("TestModelSystem2", session.Project.ModelSystemStructure[1].Name);
            }
        }

        private ModelSystem CreateTestModelSystem(XTMFRuntime runtime)
        {
            var controller = runtime.ModelSystemController;
            controller.Delete("TestModelSystem");
            var modelSystem = controller.LoadOrCreate("TestModelSystem");
            string? error = null;
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
