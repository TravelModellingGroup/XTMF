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
using System.IO;
using System.Reflection;
using XTMF;

namespace TMG.GTAModel.DataUtility
{
    internal static class UniversalRead<TDestination>
    {
        internal static IRead<TDestination, TSource> CreateReader<TSource>(TSource instanceOfObject, string variableName)
        {
            if (instanceOfObject == null)
            {
                throw new XTMFRuntimeException(null, "Unable to create a reader from a null instance!");
            }
            var variableNameParts = variableName.Split('.');
            if (!VerrifyType(instanceOfObject.GetType(), variableNameParts, out Type[] sourceType, out bool[] property))
            {
                throw new XTMFRuntimeException(null, "Unable to find \"" + variableName + "\" inside of a \"" + ((Type)instanceOfObject.GetType()).FullName + "\"");
            }

            return CreateReader<TSource>(instanceOfObject.GetType(), variableNameParts, sourceType, property);
        }

        private static CodeNamespace AddNamespaces(CodeCompileUnit unit)
        {
            var namespaceXTMF = new CodeNamespace("XTMF.DataUtilities.Generated");
            namespaceXTMF.Imports.Add(new CodeNamespaceImport("System"));
            namespaceXTMF.Imports.Add(new CodeNamespaceImport("TMG.GTAModel.DataUtility"));
            unit.Namespaces.Add(namespaceXTMF);
            return namespaceXTMF;
        }

        private static void AddReferences(CodeCompileUnit unit)
        {
            var moduleDirectory = GetModuleDirectory();
            var callingAssembly = Assembly.GetCallingAssembly();
            unit.ReferencedAssemblies.Add(callingAssembly.CodeBase.Substring(8));
            foreach (var t in callingAssembly.GetReferencedAssemblies())
            {
                var localName = Path.Combine(moduleDirectory, t.Name + ".dll");
                if (File.Exists(localName))
                {
                    unit.ReferencedAssemblies.Add(localName);
                }
                else
                {
                    unit.ReferencedAssemblies.Add(t.Name + ".dll");
                }
            }
        }

        private static CodeAssignStatement CreateAssingmentStatement(CodeParameterDeclarationExpression result, CodeParameterDeclarationExpression from,
            Type instanceType, string[] variableNameParts, Type[] sourceType, bool[] property)
        {
            var root = new CodeCastExpression(instanceType, new CodeVariableReferenceExpression(from.Name));
            if (sourceType.Length > 0)
            {
                var expression = Get(variableNameParts, sourceType, property, root, 0);
                return new CodeAssignStatement(new CodeVariableReferenceExpression(result.Name), expression);
            }
            return new CodeAssignStatement(new CodeVariableReferenceExpression(result.Name), root);
        }

        private static IRead<TDestination, TSource> CreateReader<TSource>(Type instanceType, string[] variableNameParts, Type[] sourceType, bool[] property)
        {
            var destinationType = typeof(TDestination);
            var unit = new CodeCompileUnit();
            AddReferences(unit);
            var namespaceXTMF = AddNamespaces(unit);
            var uniqueId = DateTime.Now.Ticks;
            var realTimeReader = new CodeTypeDeclaration(String.Format("RealtimeCompiledReader{0}", uniqueId));
            realTimeReader.BaseTypes.Add(new CodeTypeReference(String.Format("IRead<{0},{1}>", destinationType.FullName, typeof(TSource))));
            var copyMethod = new CodeMemberMethod
            {
                Name = "Read",
                Attributes = MemberAttributes.Public,
                ReturnType = new CodeTypeReference(typeof(bool))
            };
            var readFrom = new CodeParameterDeclarationExpression(typeof(TSource), "readFrom") { Direction = FieldDirection.In };
            var storeIn = new CodeParameterDeclarationExpression(typeof(TDestination), "result") { Direction = FieldDirection.Out };
            copyMethod.Parameters.AddRange(new[] { readFrom, storeIn });
            copyMethod.Statements.Add(CreateAssingmentStatement(storeIn, readFrom, instanceType, variableNameParts, sourceType, property));
            copyMethod.Statements.Add(new CodeMethodReturnStatement(new CodePrimitiveExpression(true)));
            realTimeReader.Members.Add(copyMethod);
            namespaceXTMF.Types.Add(realTimeReader);
            var compiler = CodeDomProvider.CreateProvider("CSharp");
            var options = new CompilerParameters
            {
                IncludeDebugInformation = false,
                GenerateInMemory = true,
                CompilerOptions = "/optimize"
            };
            /*using ( StreamWriter writer = new StreamWriter( "code.cs" ) )
            {
                compiler.GenerateCodeFromCompileUnit( unit, writer, new CodeGeneratorOptions() );
                return null;
            }*/
            var results = compiler.CompileAssemblyFromDom(options, unit);
            if (results.Errors.Count != 0)
            {
                throw new XTMFRuntimeException(null, results.Errors[0].ToString());
            }

            var assembly = results.CompiledAssembly;
            var theClass = assembly.GetType(String.Format("XTMF.DataUtilities.Generated.RealtimeCompiledReader{0}", uniqueId));
            var constructor = theClass.GetConstructor(new Type[0]);
            var output = constructor?.Invoke(new object[0]);
            return output as IRead<TDestination, TSource>;
        }

        private static CodeExpression Get(string[] variableNameParts, Type[] sourceType, bool[] property, CodeExpression root, int index)
        {
            CodeExpression expression = new CodeCastExpression(new CodeTypeReference(sourceType[index]),
                (property[index] ? new CodePropertyReferenceExpression(root, variableNameParts[index])
                : (CodeExpression)new CodeFieldReferenceExpression(root, variableNameParts[index])));
            if (index < variableNameParts.Length - 1)
            {
                return Get(variableNameParts, sourceType, property, expression, index + 1);
            }
            return expression;
        }

        private static string GetModuleDirectory()
        {
            var programPath = Path.GetFullPath(Assembly.GetEntryAssembly().CodeBase.Replace("file:///", String.Empty));
            return Path.Combine(Path.GetDirectoryName(programPath), "Modules");
        }

        private static bool VerrifyType(Type instanceType, string[] parts, out Type[] sourceType, out bool[] property)
        {
            sourceType = new Type[parts.Length];
            property = new bool[parts.Length];
            for (var i = 0; i < parts.Length; i++)
            {
                var field = instanceType.GetField(parts[i]);
                if (field == null)
                {
                    var p = instanceType.GetProperty(parts[i]);
                    if (p == null)
                    {
                        sourceType = null;
                        property = null;
                        return false;
                    }
                    instanceType = p.PropertyType;
                    sourceType[i] = instanceType;
                    property[i] = true;
                }
                else
                {
                    instanceType = field.FieldType;
                    sourceType[i] = instanceType;
                    property[i] = false;
                }
            }
            return true;
        }
    }
}