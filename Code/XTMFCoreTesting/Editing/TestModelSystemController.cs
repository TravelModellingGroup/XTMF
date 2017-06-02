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
    public class TestModelSystemController
    {
        [TestMethod]
        public void TestNoDeleteDuringSession()
        {
            var runtime = TestXTMFCore.CreateRuntime();
            var controller = runtime.ModelSystemController;
            var msName = "TestModelSystem";
            controller.Delete( msName );
            var ms = controller.LoadOrCreate( msName );
            Assert.AreNotEqual( null, ms, "The model system 'TestModelSystem' was null!" );
            string error = null;
            Assert.IsTrue( controller.Delete( ms, ref error ), "We were unable to delete a model system that should have existed!" );
            ms = controller.LoadOrCreate( msName );
            using (controller.EditModelSystem( ms ))
            {
                Assert.IsFalse( controller.Delete( ms, ref error ), "Even though the model system had an editing session it was deleted!" );
            }
            Assert.IsTrue( controller.Delete( ms, ref error ), "Even though the model system was no longer being editing it was not deleted!" );
        }

        [TestMethod]
        public void TestImportModelSystem()
        {
            var runtime = TestXTMFCore.CreateRuntime();
            var controller = runtime.ModelSystemController;
            var importName = "TestImportModelSystem";
            string error = null;
            controller.Delete( importName );
            Assert.IsNull( controller.Load( importName ) );
            Assert.IsTrue( controller.ImportModelSystem( "TestImportModelSystem.xml", false, ref error ), error );
            Assert.IsNotNull( controller.Load( importName ) );
        }
    }
}
