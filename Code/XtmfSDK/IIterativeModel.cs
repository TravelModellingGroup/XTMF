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

using XTMF;

namespace YourLibraryNameHere
{
    /// <summary>
    /// This is an example interface to extend the functionality of a model.
    ///
    /// Here we are going to add more requirements for a model, in this case
    /// so that it can handle iteration.
    ///
    /// In your Model Systems you can then have a field of type "IIterationModel"
    /// and then only models that provide this interface can be loaded into it by
    /// XTMF.
    /// </summary>
    /// <typeparam name="T">Here we provide a generic type for whatever data needs to be processed</typeparam>
    public interface IIterativeModel<T> : IModule
    {
        /// <summary>
        /// Another example of something that is useful is a call at the end of an iteration.
        /// This could be used to release files or to save the data that it has collected.
        /// </summary>
        void EndIteration();

        /// <summary>
        /// This could be used for saving things that might have been collecting
        /// over multiple iterations.
        /// </summary>
        void PostRun();

        /// <summary>
        /// This could be called to preload data that does not need to be loaded each iteration
        /// </summary>
        void PreRun();

        /// <summary>
        /// Process the given data
        /// </summary>
        /// <param name="t">The type of the data that needs to be processed</param>
        void Process(T t);

        /// <summary>
        /// For an example, here we have a method that gets called at the start of an iteration
        /// (the calling of such is up to the IModelSystem code).
        /// This could be used to load data.
        /// </summary>
        void StartIteration();
    }
}