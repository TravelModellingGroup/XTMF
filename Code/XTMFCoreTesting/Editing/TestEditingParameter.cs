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

using System.Collections.Generic;
using System.Linq;
using XTMF.Testing.Modules.Editing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace XTMF.Testing.Editing
{
    [TestClass]
    public class TestEditingParameter
    {
        [TestMethod]
        public void TestEditParameter()
        {
            var runtime = TestXTMFCore.CreateRuntime();
            var controller = runtime.ModelSystemController;
            var msName = "TestModelSystem";
            controller.Delete( msName );
            var ms = controller.LoadOrCreate( msName );
            Assert.AreNotEqual( null, ms, "The model system 'TestModelSystem' was null!" );
            using (var session = controller.EditModelSystem( ms ))
            {
                var model = session.ModelSystemModel;
                Assert.IsNotNull( model, "No model system model was created!" );
                ModelSystemStructureModel root = model.Root;
                Assert.IsNotNull( root, "No root object was made!" );

                root.Type = typeof(TestModelSystemTemplate);

                var parameters = root.Parameters.GetParameters();
                Assert.IsNotNull( parameters, "There are no parameters for our test model system template!" );
                var inputDirectory = GetParameter( parameters, "Input Directory" );
                Assert.IsNotNull( inputDirectory, "There was no parameter called input directory." );
                string? error = null;
                var previousValue = inputDirectory.Value;
                var newValue = "NewValue";
                Assert.IsTrue( inputDirectory.SetValue( newValue, ref error ), "The assignment of a value to a string somehow failed!" );
                Assert.AreEqual( newValue, inputDirectory?.Value, "The valid value was not stored in the parameter!" );
                Assert.IsTrue( session.Undo( ref error ), "There should have been a command that could be undone!" );
                Assert.AreEqual( previousValue, inputDirectory?.Value, "The undo did not restore the previous value." );
                Assert.IsTrue( session.Redo( ref error ), "There should have been a command to have redone!" );
                Assert.AreEqual( newValue, inputDirectory?.Value, "The valid value was not stored in the parameter after the redo!" );
            }
        }

        [TestMethod]
        public void TestEditParameterToDefault()
        {
            var runtime = TestXTMFCore.CreateRuntime();
            var controller = runtime.ModelSystemController;
            var msName = "TestModelSystem";
            controller.Delete( msName );
            var ms = controller.LoadOrCreate( msName );
            Assert.AreNotEqual( null, ms, "The model system 'TestModelSystem' was null!" );
            using (var session = controller.EditModelSystem( ms ))
            {
                var model = session.ModelSystemModel;
                Assert.IsNotNull( model, "No model system model was created!" );
                ModelSystemStructureModel root = model.Root;
                Assert.IsNotNull( root, "No root object was made!" );

                root.Type = typeof(TestModelSystemTemplate);

                var parameters = root.Parameters.GetParameters();
                Assert.IsNotNull( parameters, "There are no parameters for our test model system template!" );
                var inputDirectory = GetParameter( parameters, "Input Directory" );
                Assert.IsNotNull( inputDirectory, "There was no parameter called input directory." );
                string? error = null;
                var previousValue = inputDirectory.Value;
                var newValue = "NewValue";
                Assert.IsTrue( inputDirectory.SetValue( newValue, ref error ), "The assignment of a value to a string somehow failed!" );
                Assert.AreEqual( newValue, inputDirectory.Value, "The valid value was not stored in the parameter!" );
                Assert.IsTrue( inputDirectory.SetToDefault( ref error ), "Set to default failed!" );
                Assert.AreEqual( previousValue, inputDirectory?.Value, "We did not revert to the previous value!" );
            }
        }

        [TestMethod]
        public void TestSettingParameterInLinkedParameter()
        {
            var runtime = TestXTMFCore.CreateRuntime();
            var controller = runtime.ModelSystemController;
            var msName = "TestModelSystem";
            controller.Delete( msName );
            var ms = controller.LoadOrCreate( msName );
            Assert.AreNotEqual( null, ms, "The model system 'TestModelSystem' was null!" );
            using (var session = controller.EditModelSystem( ms ))
            {
                var modelSystem = session.ModelSystemModel;
                var linkedParameters = modelSystem.LinkedParameters;
                Assert.AreEqual( 0, linkedParameters.Count, "The model system already had a linked parameter before we added any!" );
                string? error = null;
                Assert.IsTrue( linkedParameters.NewLinkedParameter( "Test", ref error ), "We failed to create our first linked parameter!" );
                Assert.AreEqual( 1, linkedParameters.Count, "After adding a linked parameter it still reports that there isn't one linked parameter." );


                var model = session.ModelSystemModel;
                Assert.IsNotNull( model, "No model system model was created!" );
                ModelSystemStructureModel root = model.Root;
                Assert.IsNotNull( root, "No root object was made!" );
                root.Type = typeof(TestModelSystemTemplate);

                var parameters = root.Parameters.GetParameters();
                Assert.IsNotNull( parameters, "There are no parameters for our test model system template!" );
                var inputDirectory = GetParameter( parameters, "Input Directory" );
                var secondaryString = GetParameter( parameters, "SecondaryString" );

                var linkedParameterList = linkedParameters.GetLinkedParameters();
                Assert.IsTrue( linkedParameterList[0].AddParameter( inputDirectory, ref error ), error );
                Assert.IsTrue( linkedParameterList[0].AddParameter( secondaryString, ref error ), error );
                string? oldValue = linkedParameterList[0].GetValue();
                string? newValue = "NewValue";
                Assert.IsTrue( linkedParameterList[0].SetValue( newValue, ref error ) );
                // assign to both with through the linked parameter
                Assert.AreEqual( newValue, linkedParameterList[0].GetValue() );
                Assert.AreEqual( newValue, inputDirectory?.Value );
                Assert.AreEqual( newValue, secondaryString?.Value );
                // assign to both using the secondary string
                Assert.IsTrue( secondaryString?.SetValue( oldValue, ref error ) );
                Assert.AreEqual( oldValue, linkedParameterList[0].GetValue() );
                Assert.AreEqual( oldValue, inputDirectory?.Value );
                Assert.AreEqual( oldValue, secondaryString?.Value );

                Assert.IsTrue( session.Undo( ref error ) );
                Assert.AreEqual( newValue, linkedParameterList[0].GetValue() );
                Assert.AreEqual( newValue, inputDirectory?.Value );
                Assert.AreEqual( newValue, secondaryString?.Value );


                Assert.IsTrue( session.Redo( ref error ) );
                Assert.AreEqual( oldValue, linkedParameterList[0].GetValue() );
                Assert.AreEqual( oldValue, inputDirectory?.Value );
                Assert.AreEqual( oldValue, secondaryString?.Value );
            }
        }

        [TestMethod]
        public void TestSettingParameterToDefaultInLinkedParameter()
        {
            var runtime = TestXTMFCore.CreateRuntime();
            var controller = runtime.ModelSystemController;
            var msName = "TestModelSystem";
            controller.Delete( msName );
            var ms = controller.LoadOrCreate( msName );
            Assert.AreNotEqual( null, ms, "The model system 'TestModelSystem' was null!" );
            using (var session = controller.EditModelSystem( ms ))
            {
                var modelSystem = session.ModelSystemModel;
                var linkedParameters = modelSystem.LinkedParameters;
                Assert.AreEqual( 0, linkedParameters.Count, "The model system already had a linked parameter before we added any!" );
                string? error = null;
                Assert.IsTrue( linkedParameters.NewLinkedParameter( "Test", ref error ), "We failed to create our first linked parameter!" );
                Assert.AreEqual( 1, linkedParameters.Count, "After adding a linked parameter it still reports that there isn't one linked parameter." );


                var model = session.ModelSystemModel;
                Assert.IsNotNull( model, "No model system model was created!" );
                ModelSystemStructureModel root = model.Root;
                Assert.IsNotNull( root, "No root object was made!" );
                root.Type = typeof(TestModelSystemTemplate);

                var parameters = root.Parameters.GetParameters();
                Assert.IsNotNull( parameters, "There are no parameters for our test model system template!" );
                var inputDirectory = GetParameter( parameters, "Input Directory" );
                var secondaryString = GetParameter( parameters, "SecondaryString" );

                var linkedParameterList = linkedParameters.GetLinkedParameters();
                Assert.IsTrue( linkedParameterList[0].AddParameter( inputDirectory, ref error ), error );
                Assert.IsTrue( linkedParameterList[0].AddParameter( secondaryString, ref error ), error );
                string? newValue = "NewValue";
                Assert.IsTrue( linkedParameterList[0].SetValue( newValue, ref error ) );
                // assign to both with through the linked parameter
                Assert.AreEqual( newValue, linkedParameterList[0].GetValue() );
                Assert.AreEqual( newValue, inputDirectory?.Value );
                Assert.AreEqual( newValue, secondaryString?.Value );
                // assign to both using the secondary string
                Assert.IsFalse( secondaryString?.SetToDefault( ref error ) );

            }
        }

        private static ParameterModel? GetParameter(IList<ParameterModel> parameters, string parameterName)
        {
            return parameters.FirstOrDefault( (p) => p.Name == parameterName );
        }
    }
}
