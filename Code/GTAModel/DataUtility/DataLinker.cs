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
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Reflection;
using XTMF;

namespace TMG.GTAModel.DataUtility
{
    /// <summary>
    /// This class provides a way to generate a runtime created object that implements the IDataLink Interface
    /// </summary>
    internal static class DataLinker
    {
        /// <summary>
        /// Check the types for copying to ensure that they are compatible
        /// </summary>
        /// <param name="destination">The type of object that we are saving to</param>
        /// <param name="destinationField">The name of the field that we are saving to</param>
        /// <param name="origin">The type of the object that we are going to copy from</param>
        /// <param name="originField">The name of the field that we are going to copy from</param>
        /// <param name="destinationFieldType">The type of the field that we are going to write into</param>
        /// <param name="originFieldType">The type of the field that we are going to read from</param>
        /// <returns>If we can copy between these types</returns>
        public static bool CheckTypes(Type destination, string destinationField, Type origin, string originField, out Type destinationFieldType, out Type originFieldType)
        {
            var destinationFieldTypeF = destination.GetField( destinationField );
            var originFieldTypeF = origin.GetField( originField );

            if ( destinationFieldTypeF == null || originFieldTypeF == null )
            {
                destinationFieldType = null;
                originFieldType = null;
                return false;
            }
            destinationFieldType = destinationFieldTypeF.FieldType;
            originFieldType = originFieldTypeF.FieldType;
            return destinationFieldType.IsAssignableFrom( originFieldType );
        }

        /// <summary>
        /// Check to make sure that an assignment is valid
        /// </summary>
        /// <param name="destinationType">The type of the object that we are going to copy into</param>
        /// <param name="destinationField">The name of the field of the object that we are going to copy into</param>
        /// <param name="originData">The type of data that we will copy into the field</param>
        /// <param name="destinationFieldType">The type of the field that we are going to copy into</param>
        /// <returns>If it is possible to copy into the field</returns>
        public static bool CheckTypes(Type destination, string destinationField, Type originData, out Type destinationFieldType)
        {
            var destinationFieldTypeF = destination.GetField( destinationField );
            if ( destinationFieldTypeF == null )
            {
                destinationFieldType = null;
                return false;
            }
            destinationFieldType = destinationFieldTypeF.FieldType;
            return destinationFieldType.IsAssignableFrom( originData );
        }

        /// <summary>
        /// This function will create a new IDataLink for the given Types and associations
        /// </summary>
        /// <typeparam name="T">The type that we will be saving into</typeparam>
        /// <param name="originTypes">The types that we will be loading from</param>
        /// <param name="Links"></param>
        /// <returns>A new IDataLink object that will automate the copying of data</returns>
        public static IDataLink<T> CreateDataLink<T>(Type[] originTypes, KeyValuePair<string, KeyValuePair<int, string>>[] Links)
        {
            Type destinationType = typeof( T );
            CodeCompileUnit unit = new CodeCompileUnit();
            unit.ReferencedAssemblies.Add( Assembly.GetCallingAssembly().Location );
            CodeNamespace namespaceXTMF = new CodeNamespace( "XTMF.DataUtilities.Generated" );
            namespaceXTMF.Imports.Add( new CodeNamespaceImport( "System" ) );
            namespaceXTMF.Imports.Add( new CodeNamespaceImport( "XTMF.DataUtilities" ) );
            unit.Namespaces.Add( namespaceXTMF );
            long uniqueID = DateTime.Now.Ticks;
            CodeTypeDeclaration transitionClass = new CodeTypeDeclaration( String.Format( "TransitionClass{0}", uniqueID ) );
            transitionClass.BaseTypes.Add( new CodeTypeReference( String.Format( "IDataLink<{0}>", destinationType.FullName ) ) );
            CodeMemberMethod CopyMethod = new CodeMemberMethod();
            CopyMethod.Name = "Copy";
            CopyMethod.Attributes = MemberAttributes.Public;
            var destintation = new CodeParameterDeclarationExpression( destinationType, "destination" );
            var origin = new CodeParameterDeclarationExpression( typeof( object[] ), "origin" );
            CopyMethod.Parameters.AddRange( new[] { destintation, origin } );
            foreach ( KeyValuePair<string, KeyValuePair<int, string>> link in Links )
            {
                //destination."link.Key" = origin[link.Value.Key]."link.Value.Value";
                Type originField, destinationField;
                if ( !CheckTypes( destinationType, link.Key, originTypes[link.Value.Key], link.Value.Value, out destinationField, out originField ) )
                {
                    throw new XTMFRuntimeException( String.Format( "Type miss-match error between {0}:{2} to {1}:{3}!",
                        originField.Name, destinationField.Name, link.Value.Value, link.Key ) );
                }
                CodeAssignStatement assign = new CodeAssignStatement(
                    new CodeFieldReferenceExpression( new CodeVariableReferenceExpression( "destination" ), link.Key ),
                    new CodeFieldReferenceExpression( new CodeCastExpression( originTypes[link.Value.Key],
                        new CodeIndexerExpression( new CodeVariableReferenceExpression( "origin" ), new CodePrimitiveExpression( link.Value.Key ) ) ), link.Value.Value )
                    );
                CopyMethod.Statements.Add( assign );
            }
            transitionClass.Members.Add( CopyMethod );
            namespaceXTMF.Types.Add( transitionClass );
            CodeDomProvider compiler = CodeDomProvider.CreateProvider( "CSharp" );
            var options = new CompilerParameters();
            options.IncludeDebugInformation = false;
            options.GenerateInMemory = true;
            var results = compiler.CompileAssemblyFromDom( options, unit );
            if ( results.Errors.Count != 0 )
            {
                throw new XTMFRuntimeException( results.Errors[0].ToString() );
            }
            var assembly = results.CompiledAssembly;
            var theClass = assembly.GetType( String.Format( "XTMF.DataUtilities.Generated.TransitionClass{0}", uniqueID ) );
            var constructor = theClass.GetConstructor( new Type[0] );
            var output = constructor.Invoke( new object[0] );
            return output as IDataLink<T>;
        }
    }
}