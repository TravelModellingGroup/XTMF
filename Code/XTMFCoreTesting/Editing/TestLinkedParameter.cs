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

using XTMF.Testing.Modules.Editing;
using Microsoft.VisualStudio.TestTools.UnitTesting;


namespace XTMF.Testing.Editing;

[TestClass]
public class TestLinkedParameter
{
    [TestMethod]
    public void TestAddingModuleToLinkedParameter()
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


        var model = session.ModelSystemModel;
        Assert.IsNotNull(model, "No model system model was created!");
        ModelSystemStructureModel root = model.Root;
        Assert.IsNotNull(root, "No root object was made!");
        root.Type = typeof(TestModelSystemTemplate);

        var parameters = root.Parameters.GetParameters();
        Assert.IsNotNull(parameters, "There are no parameters for our test model system template!");
        var inputDirectory = GetParameter(parameters, "Input Directory");

        var linkedParameterList = linkedParameters.GetLinkedParameters();
        Assert.IsTrue(linkedParameterList[0].AddParameter(inputDirectory, ref error), error);
        var moduleParametersLinked = linkedParameterList[0].GetParameters();
        Assert.AreEqual(1, moduleParametersLinked.Count, "The number of module parameters that are linked should just be one!");
        Assert.IsTrue(session.Undo(ref error), "We were unable to undo!");
        moduleParametersLinked = linkedParameterList[0].GetParameters();
        Assert.AreEqual(0, moduleParametersLinked.Count, "The number of module parameters that are linked should just be zero after undo!");
        Assert.IsTrue(session.Redo(ref error), "We were unable to redo!");
        moduleParametersLinked = linkedParameterList[0].GetParameters();
        Assert.AreEqual(1, moduleParametersLinked.Count, "The number of module parameters that are linked should just be one after redo!");
    }


    [TestMethod]
    public void TestRemovingModuleToLinkedParameter()
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


        var model = session.ModelSystemModel;
        Assert.IsNotNull(model, "No model system model was created!");
        ModelSystemStructureModel root = model.Root;
        Assert.IsNotNull(root, "No root object was made!");
        root.Type = typeof(TestModelSystemTemplate);

        var parameters = root.Parameters.GetParameters();
        Assert.IsNotNull(parameters, "There are no parameters for our test model system template!");
        var inputDirectory = GetParameter(parameters, "Input Directory");

        var linkedParameterList = linkedParameters.GetLinkedParameters();
        Assert.IsTrue(linkedParameterList[0].AddParameter(inputDirectory, ref error), error);
        var moduleParametersLinked = linkedParameterList[0].GetParameters();
        Assert.AreEqual(1, moduleParametersLinked.Count, "The number of module parameters that are linked should just be one!");

        Assert.IsTrue(linkedParameterList[0].RemoveParameter(moduleParametersLinked[0], ref error));
        Assert.IsFalse(linkedParameterList[0].RemoveParameter(moduleParametersLinked[0], ref error), "We got a true for removing a parameter that was already removed");

        moduleParametersLinked = linkedParameterList[0].GetParameters();
        Assert.AreEqual(0, moduleParametersLinked.Count, "No module parameters should be left.");

        Assert.IsTrue(session.Undo(ref error));
        moduleParametersLinked = linkedParameterList[0].GetParameters();
        Assert.AreEqual(1, moduleParametersLinked.Count, "No module parameter should be returned after undo.");

        Assert.IsTrue(session.Redo(ref error));
        moduleParametersLinked = linkedParameterList[0].GetParameters();
        Assert.AreEqual(0, moduleParametersLinked.Count, "No module parameters should be left after redo.");
    }


    [TestMethod]
    public void TestSettingValueForLinkedParameter()
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


        var model = session.ModelSystemModel;
        Assert.IsNotNull(model, "No model system model was created!");
        ModelSystemStructureModel root = model.Root;
        Assert.IsNotNull(root, "No root object was made!");
        root.Type = typeof(TestModelSystemTemplate);

        var parameters = root.Parameters.GetParameters();
        Assert.IsNotNull(parameters, "There are no parameters for our test model system template!");
        var inputDirectory = GetParameter(parameters, "Input Directory");

        var linkedParameterList = linkedParameters.GetLinkedParameters();
        Assert.IsTrue(linkedParameterList[0].AddParameter(inputDirectory, ref error), error);
        string? oldValue = linkedParameterList[0].GetValue();
        string? newValue = "NewValue";
        Assert.IsTrue(linkedParameterList[0].SetValue(newValue, ref error));

        Assert.AreEqual(newValue, linkedParameterList[0].GetValue());
        Assert.AreEqual(newValue, inputDirectory?.Value);

        Assert.IsTrue(session.Undo(ref error));
        Assert.AreEqual(oldValue, linkedParameterList[0].GetValue());
        Assert.AreEqual(oldValue, inputDirectory?.Value);

        Assert.IsTrue(session.Redo(ref error));
        Assert.AreEqual(newValue, linkedParameterList[0].GetValue());
        Assert.AreEqual(newValue, inputDirectory?.Value);
    }

    private static ParameterModel? GetParameter(IList<ParameterModel> parameters, string parameterName)
    {
        return parameters.FirstOrDefault( (p) => p.Name == parameterName );
    }
}
