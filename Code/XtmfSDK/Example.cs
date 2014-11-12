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
using System;
using XTMF;

namespace YourLibraryNameHere
{
    /// <summary>
    /// Here is an example of how to write a model for XTMF
    /// The first step is to extend an interface that extends IModel so
    /// XTMF knows to look and configure this class.  For this example
    /// we are  going to extend IModelSystem.
    /// This will provide an entry point for XTMF to run your model after
    /// all of your parameters have been loaded.
    /// </summary>
    [ModuleInformation(Description =
        @"The text here will be displayed in the XTMF GUI's help window.  It can be in full HTML and link to online resources.  If you are going to do that make sure to use target='blank' in our anchor tags.")]
    public class AnExampleModel : IModelSystemTemplate
    {
        /// <summary>
        /// This is an example of how to include a sub-model in your model system (or model).
        /// We add the attributes to help describe what this field is going to be used for.
        /// This information will be taken by XTMF GUI in order to help provide more information
        /// to the user.
        ///
        /// If a model is not required, set Required to false so when a project is created no model
        /// will be required for execution.
        /// </summary>
        [SubModelInformation( Required = true, Description = "This is an example of how to include a model in XTMF" )]
        public IIterativeModel<int> AnExampleModelField;

        /// <summary>
        /// To save processing times we will pre-process the progress colour.
        /// This will produce a green base colour for our progress.
        /// Values are (0-255,0-255,0-255) for red, green, and blue.
        /// </summary>
        private Tuple<byte, byte, byte> _ProgressColour = new Tuple<byte, byte, byte>( 50, 150, 50 );

        /// <summary>
        /// This provides a way for XTMF to create an object of your model.
        /// Your parameters will not have been loaded at this point yet so please do not
        /// try to use them yet.
        /// </summary>
        public AnExampleModel()
        {
        }

        /// <summary>
        /// Providing a constructor that takes an XTMF.IConfiguration will allow you to have access to all
        /// of the information in your XTMF installation.  If XTMF detects this constructor it will select it
        /// by default over the constructor with no parameters.
        /// </summary>
        /// <param name="xtmfConfigurationObject">An object that provides all of the configuration information of the XTMF installation</param>
        public AnExampleModel(IConfiguration xtmfConfigurationObject)
        {
        }

        /// <summary>
        /// This property
        /// </summary>
        [Parameter( "Input Base Directory", "Input", "The directory relative to the Run Directory where all of the input is kept." )]
        public string InputBaseDirectory
        {
            get;
            set;
        }

        /// <summary>
        /// This is the name that XTMF sets to describe what this model is being
        /// used as.  You do not need to worry about this at all as it will be
        /// automatically set for you.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        ///
        /// </summary>
        [Parameter( "Output Base Directory", "Output", "The directory relative to the Run Directory where all of the output is kept." )]
        public string OutputBaseDirectory
        {
            get;
            set;
        }

        /// <summary>
        /// This provides a way to show the current progress of your model.
        /// XTMF Gui's can use this to show the progress of your model.
        /// Values range from 0 to 1.
        /// </summary>
        public float Progress { get; private set; }

        /// <summary>
        /// This will let an XTMF Gui to find what colour to use for your model's progress bar.
        /// You can provide extra visual feedback such as showing that an error has occurred
        /// by changing this to another colour.
        /// Values are (0-255,0-255,0-255) for red, green, and blue.
        /// </summary>
        public Tuple<byte, byte, byte> ProgressColour
        {
            get { return this._ProgressColour; }
        }

        /// <summary>
        ///
        /// </summary>
        /// <returns></returns>
        public bool ExitRequest()
        {
            /*
             * TODO: Add your logic here for exiting early, we will return false for now since there is no
             * way to exit at the moment.
             */
            return false;
        }

        /// <summary>
        /// This is called before the start method as a way to check that all of the parameters that are selected
        /// are in fact valid for this module.
        /// </summary>
        /// <param name="error">A string that should be assigned a detailed error</param>
        /// <returns>If the validation was successful or if there was a problem</returns>
        public bool RuntimeValidation(ref string error)
        {
            /* Check for all of the possible errors that could happen and if one occurs return false
               This is the point that you should check to make sure that your files exist
               and that your parent modules are configured the way that you need them.
             * It is important to note that the children modules will not have had their RuntimeValidation run yet,
             * and it is not always the case that siblings to a common module will have had their RuntimeValidation code run.
             *
             * So at this point only check the current module and your direct Ancestors.
            */
            return true;
        }

        /// <summary>
        /// This is the starting point for your model system.
        /// At this point all of your parameters will have been loaded, and Properties/Fields that are of type
        /// IModel will have been loaded with the correct objects loaded from the XTMF Project.
        /// </summary>
        public void Start()
        {
            throw new NotImplementedException();
        }
    }
}