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
using XTMF.Testing.Modules.Editing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace XTMF.Testing.Editing
{
    [TestClass]
    public class TestProjectEditingSession
    {
        [TestMethod]
        public void TestAddingModelSystem()
        {
            var runtime = TestXTMFCore.CreateRuntime();
            var controller = runtime.ProjectController;
            string error = null;
            controller.DeleteProject( "TestProject", ref error );
            Project project;
            Assert.IsTrue( ( project = controller.LoadOrCreate( "TestProject", ref error ) ) != null );
            using (var session = controller.EditProject( project ))
            {
                var testModelSystem = CreateTestModelSystem( runtime );
                Assert.IsTrue( session.AddModelSystem( testModelSystem, ref error ) );
                Assert.AreEqual( 1, project.ModelSystemStructure.Count );
            }
        }

        [TestMethod]
        public void TestRemovingModelSystem()
        {
            var runtime = TestXTMFCore.CreateRuntime();
            var controller = runtime.ProjectController;
            string error = null;
            controller.DeleteProject( "TestProject", ref error );
            Project project;
            Assert.IsTrue( ( project = controller.LoadOrCreate( "TestProject", ref error ) ) != null );
            using (var session = controller.EditProject( project ))
            {
                var testModelSystem = CreateTestModelSystem( runtime );
                Assert.IsTrue( session.AddModelSystem( testModelSystem, ref error ) );
                Assert.AreEqual( 1, project.ModelSystemStructure.Count );
                Assert.IsTrue( session.RemoveModelSystem( 0, ref error ) );
                Assert.IsTrue( session.AddModelSystem( testModelSystem, ref error ) );
                using (session.EditModelSystem( 0 ))
                {
                    Assert.IsFalse( session.RemoveModelSystem( 0, ref error ) );
                }
                // make sure it has a valid index ( under and over )
                Assert.IsFalse( session.RemoveModelSystem( -1, ref error ) );
                Assert.IsFalse( session.RemoveModelSystem( 1, ref error ) );
                // actually remove it
                Assert.IsTrue( session.RemoveModelSystem( 0, ref error ), error );
                // make sure that you can not remove a model system if it doesn't exist
                Assert.IsFalse( session.RemoveModelSystem( 0, ref error ) );
            }
        }

        [TestMethod]
        public void TestMovingModelSystems()
        {
            var runtime = TestXTMFCore.CreateRuntime();
            var controller = runtime.ProjectController;
            string error = null;
            controller.DeleteProject( "TestProject", ref error );
            Project project;
            Assert.IsTrue( ( project = controller.LoadOrCreate( "TestProject", ref error ) ) != null );
            using (var session = controller.EditProject( project ))
            {
                // create and add our two model systems into the project
                var testModelSystem = CreateTestModelSystem( runtime );
                Assert.IsTrue( session.AddModelSystem( testModelSystem, ref error ) );
                Assert.AreEqual( 1, project.ModelSystemStructure.Count );
                Assert.IsTrue( session.AddModelSystem( testModelSystem, ref error ) );
                Assert.AreEqual( 2, project.ModelSystemStructure.Count );
                var oldModelSystems = project.ModelSystemStructure.ToList();
                // try invalid moves
                Assert.IsFalse( session.MoveModelSystem( -1, -1, ref error ) );
                Assert.IsFalse( session.MoveModelSystem( -1, 0, ref error ) );
                Assert.IsFalse( session.MoveModelSystem( 0, -1, ref error ) );
                // actually move
                Assert.IsTrue( session.MoveModelSystem( 0, 1, ref error ), error );
                Assert.AreEqual( oldModelSystems[0], project.ModelSystemStructure[1], "The 0th model system is not the same as the move 1th model system!" );
                Assert.AreEqual( oldModelSystems[1], project.ModelSystemStructure[0], "The 1th model system is not the same as the move 0th model system!" );
            }
        }

        private ModelSystem CreateTestModelSystem(XTMFRuntime runtime)
        {
            var controller = runtime.ModelSystemController;
            controller.Delete( "TestModelSystem" );
            var modelSystem = controller.LoadOrCreate( "TestModelSystem" );
            string error = null;
            using (var session = controller.EditModelSystem( modelSystem ))
            {
                var root = session.ModelSystemModel.Root;
                root.Type = typeof(TestModelSystemTemplate);
                Assert.IsTrue( session.Save( ref error ) );
            }
            return modelSystem;
        }

        [TestMethod]
        public void TestSettingModelSystemRootInAProject()
        {
            string error = null;
            var runtime = TestXTMFCore.CreateRuntime();
            var projectController = runtime.ProjectController;
            var projectName = "TestProject";
            var msName = "TestModelSystem";
            var alternateName = "TestModelSystemName2";
            Project project;
            Assert.IsTrue(projectController.DeleteProject(projectName, ref error), error);
            Assert.IsTrue((project = projectController.LoadOrCreate(projectName, ref error)) != null, error);
            using (var projectSession = projectController.EditProject(project))
            {
                var testModelSystem = CreateTestModelSystem(runtime);
                Assert.IsTrue(projectSession.AddModelSystem(testModelSystem, msName, ref error), error);
                using (var session = projectSession.EditModelSystem(0))
                {
                    var model = session.ModelSystemModel;
                    Assert.AreEqual(msName, model.Name, "The initial name of the model system was not set correctly.");
                    Assert.IsTrue(model.Root.SetName(alternateName, ref error), error);
                    Assert.AreEqual(alternateName, model.Name, "The assigned name of the model system was not updated.");
                    Assert.IsTrue(session.Undo(ref error), error);
                    Assert.AreEqual(msName, model.Name, "The undone name of the model system was not updated.");
                    Assert.IsTrue(session.Redo(ref error), error);
                    Assert.AreEqual(alternateName, model.Name, "The undone name of the model system was not updated.");
                }
            }
        }
    }
}
