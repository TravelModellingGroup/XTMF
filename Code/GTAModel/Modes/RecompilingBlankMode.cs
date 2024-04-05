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
using System.IO;
using System.Reflection;
using Datastructure;
using TMG.Modes;
using XTMF;
// ReSharper disable BitwiseOperatorOnEnumWithoutFlags

namespace TMG.GTAModel.Modes
{
    public class RecompilingBlankMode : IUtilityComponentMode
    {
        [SubModelInformation(Description = "Used to test for mode feasibility.", Required = false)]
        public ICalculation<Pair<IZone, IZone>, bool> FeasibilityCalculation;

        [RunParameter("Save Mode Code", false, "Should the code for this mode be included in the run's output?")]
        public bool IncludeCode;

        [ParentModel]
        public IModule Parent;

        [Parameter("Demographic Category Feasible", 1f, "(Automated by IModeParameterDatabase)\r\nIs the currently processing demographic category feasible?")]
        public float CurrentlyFeasible { get; set; }

        [RunParameter("Mode name", "", "The name of this mode.  It should be unique to every other mode.")]
        public string ModeName { get; set; }

        public string Name { get; set; }

        public float Progress
        {
            get { return 0; }
        }

        public Tuple<byte, byte, byte> ProgressColour
        {
            get { return null; }
        }

        [SubModelInformation(Description = "The components used to build the utility of the mode.", Required = false)]
        public List<IUtilityComponent> UtilityComponents { get; set; }

        public float CalculateV(IZone origin, IZone destination, Time time)
        {
            throw new XTMFRuntimeException(this, "This method should never be actually called.");
        }

        public bool Feasible(IZone origin, IZone destination, Time time)
        {
            throw new XTMFRuntimeException(this, "This method should never be actually called.");
        }

        public bool RuntimeValidation(ref string error)
        {
            if (!CreateOptimizedMode(out IUtilityComponentMode optimizedMode, ref error))
            {
                return false;
            }
            if (!RemoveUsThenAddToParent(optimizedMode, ref error))
            {
                return false;
            }
            if (!AttachUtilityComponentObjects(optimizedMode))
            {
                return false;
            }
            return true;
        }

        private static CodeNamespace AddNamespaces(CodeCompileUnit unit)
        {
            CodeNamespace namespaceXTMF = new("TMG.Modes.Generated");
            namespaceXTMF.Imports.Add(new CodeNamespaceImport("System"));
            namespaceXTMF.Imports.Add(new CodeNamespaceImport("XTMF"));
            namespaceXTMF.Imports.Add(new CodeNamespaceImport("TMG"));
            namespaceXTMF.Imports.Add(new CodeNamespaceImport("TMG.Modes"));
            namespaceXTMF.Imports.Add(new CodeNamespaceImport("TMG.GTAModel.DataUtility"));
            unit.Namespaces.Add(namespaceXTMF);
            return namespaceXTMF;
        }

        private static void AddReferences(CodeCompileUnit unit)
        {
            var moduleDirectory = GetModuleDirectory();
            var callingAssembly = Assembly.GetCallingAssembly();
            unit.ReferencedAssemblies.Add(callingAssembly.Location.Substring(8));
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

        private static string GetModuleDirectory()
        {
            string programPath;
            var codeBase = Assembly.GetEntryAssembly().Location;
            try
            {
                programPath = Path.GetFullPath(codeBase);
            }
            catch
            {
                programPath = codeBase.Replace("file:///", String.Empty);
            }
            return Path.Combine(Path.GetDirectoryName(programPath), "Modules");
        }

        private void AddProperty(CodeTypeDeclaration modeClass, string name, Type type, object value, CodeAttributeDeclaration attribute = null)
        {
            var backendName = "_Generated_" + name;
            var backendVariable = new CodeMemberField(type, backendName)
            {
                Attributes = MemberAttributes.Private | MemberAttributes.Final,
                InitExpression = new CodePrimitiveExpression(value)
            };
            var property = new CodeMemberProperty
            {
                Name = name
            };
            if (attribute != null)
            {
                property.CustomAttributes.Add(attribute);
            }
            property.HasGet = true;
            property.HasSet = true;
            property.Attributes = MemberAttributes.Public | MemberAttributes.Final;
            property.Type = new CodeTypeReference(type);
            property.GetStatements.Add(new CodeMethodReturnStatement(new CodeFieldReferenceExpression(new CodeThisReferenceExpression(), backendName)));
            property.SetStatements.Add(
                new CodeAssignStatement(new CodeFieldReferenceExpression(new CodeThisReferenceExpression(), backendName)
                , new CodePropertySetValueReferenceExpression()));
            modeClass.Members.Add(backendVariable);
            modeClass.Members.Add(property);
        }

        private void AttachBasicModeProperties(CodeTypeDeclaration modeClass)
        {
            AddProperty(modeClass, "ModeName", typeof(string), ModeName);
            AddProperty(modeClass, "name", typeof(string), Name);
            AddProperty(modeClass, "Progress", typeof(float), 0.0f);
            AddProperty(modeClass, "CurrentlyFeasible", typeof(float), CurrentlyFeasible, new CodeAttributeDeclaration(new CodeTypeReference(typeof(RunParameterAttribute)),
                new CodeAttributeArgument(new CodePrimitiveExpression("Demographic Category Feasible")), new CodeAttributeArgument(new CodePrimitiveExpression(1.0f)),
                    new CodeAttributeArgument(new CodePrimitiveExpression("This is generated just ignore me"))));
            AddProperty(modeClass, "UtilityComponents", typeof(List<IUtilityComponent>), null);
            AddProperty(modeClass, "ProgressColour", typeof(Tuple<byte, byte, byte>), null);
        }

        private void AttachConstructor()
        {
            CodeConstructor constructor = new()
            {
                Attributes = MemberAttributes.Public
            };
        }

        private void AttachFeasible(CodeTypeDeclaration modeClass)
        {
            CodeMemberMethod feasibleMethod = new()
            {
                Name = "Feasible",
                Attributes = MemberAttributes.Final | MemberAttributes.Public,
                ReturnType = new CodeTypeReference(typeof(bool))
            };
            feasibleMethod.Parameters.Add(new CodeParameterDeclarationExpression(typeof(IZone), "origin"));
            feasibleMethod.Parameters.Add(new CodeParameterDeclarationExpression(typeof(IZone), "destination"));
            feasibleMethod.Parameters.Add(new CodeParameterDeclarationExpression(typeof(Time), "time"));
            if (FeasibilityCalculation != null)
            {
                modeClass.Members.Add(new CodeMemberField(FeasibilityCalculation.GetType(), "FeasibilityCalculation"));
                feasibleMethod.Statements.Add(new CodeConditionStatement(
                    // expression
                    new CodeMethodInvokeExpression(new CodeMethodReferenceExpression(new CodeFieldReferenceExpression(new CodeThisReferenceExpression(), "FeasibilityCalculation"),
                        "ProduceResult"),
                        new CodeObjectCreateExpression(new CodeTypeReference(typeof(Pair<IZone, IZone>)), new CodeArgumentReferenceExpression("origin"),
                        new CodeArgumentReferenceExpression("destination"))
                        ),
                    // if true
                    new CodeStatement[0],
                    // if false
                    new CodeStatement[]
                    {
                        new CodeMethodReturnStatement(
                                new CodePrimitiveExpression(false)
                            )
                    }));
            }
            feasibleMethod.Statements.Add(
                new CodeMethodReturnStatement(
                    new CodeBinaryOperatorExpression(new CodePropertyReferenceExpression(new CodeThisReferenceExpression(), nameof(CurrentlyFeasible)), CodeBinaryOperatorType.GreaterThan,
                        new CodePrimitiveExpression(0))
                    )
                );

            modeClass.Members.Add(feasibleMethod);
        }

        private void AttachRuntimeValidation(CodeTypeDeclaration modeClass)
        {
            CodeMemberMethod runtimeValidation = new()
            {
                Name = "RuntimeValidation",
                Attributes = MemberAttributes.Public | MemberAttributes.Final,
                ReturnType = new CodeTypeReference(typeof(bool))
            };
            runtimeValidation.Parameters.Add(new CodeParameterDeclarationExpression(typeof(string), "error") { Direction = FieldDirection.Ref });
            runtimeValidation.Statements.Add(new CodeMethodReturnStatement(new CodePrimitiveExpression(true)));
            modeClass.Members.Add(runtimeValidation);
        }

        private void AttachUtilityComponentInitializationMethod(CodeTypeDeclaration modeClass)
        {
            CodeMemberMethod initializeUtilityComponents = new()
            {
                Name = "InitialzeUtilities",
                Attributes = MemberAttributes.Public | MemberAttributes.Final
            };
            if (FeasibilityCalculation != null)
            {
                initializeUtilityComponents.Parameters.Add(new CodeParameterDeclarationExpression(FeasibilityCalculation.GetType(), "feasibilityCalculation"));
            }
            initializeUtilityComponents.Parameters.Add(new CodeParameterDeclarationExpression(typeof(List<IUtilityComponent>), "utilityComponents"));

            CodeMemberMethod calculateV = new()
            {
                Name = "CalculateV",
                ReturnType = new CodeTypeReference(typeof(float)),
                Attributes = MemberAttributes.Public | MemberAttributes.Final
            };
            calculateV.Parameters.Add(new CodeParameterDeclarationExpression(typeof(IZone), "origin"));
            calculateV.Parameters.Add(new CodeParameterDeclarationExpression(typeof(IZone), "destination"));
            calculateV.Parameters.Add(new CodeParameterDeclarationExpression(typeof(Time), "time"));

            initializeUtilityComponents.Statements.Add(new CodeAssignStatement(new CodePropertyReferenceExpression(new CodeThisReferenceExpression(), nameof(UtilityComponents)),
                new CodeArgumentReferenceExpression("utilityComponents")));
            if (FeasibilityCalculation != null)
            {
                initializeUtilityComponents.Statements.Add(new CodeAssignStatement(new CodePropertyReferenceExpression(new CodeThisReferenceExpression(), "FeasibilityCalculation"),
                    new CodeArgumentReferenceExpression("feasibilityCalculation")));
            }
            calculateV.Statements.Add(new CodeVariableDeclarationStatement(typeof(float), "v", new CodePrimitiveExpression(0.0f)));
            for (int i = 0; i < UtilityComponents.Count; i++)
            {
                SetupUtilityComponentField(modeClass, initializeUtilityComponents, calculateV, i);
            }
            calculateV.Statements.Add(new CodeMethodReturnStatement(new CodeVariableReferenceExpression("v")));
            modeClass.Members.Add(calculateV);
            modeClass.Members.Add(initializeUtilityComponents);
        }

        private bool AttachUtilityComponentObjects(IUtilityComponentMode optimizedMode)
        {
            var initFunction = optimizedMode.GetType().GetMethod("InitialzeUtilities");
            if (FeasibilityCalculation != null)
            {
                initFunction.Invoke(optimizedMode, new object[] { FeasibilityCalculation, UtilityComponents });
            }
            else
            {
                initFunction.Invoke(optimizedMode, new object[] { UtilityComponents });
            }
            return true;
        }

        private bool CompileMode(CodeCompileUnit unit, out IUtilityComponentMode optimizedMode, ref string error)
        {
            var options = new CompilerParameters
            {
                IncludeDebugInformation = false,
                GenerateInMemory = true,
                CompilerOptions = "/optimize"
            };
            CodeDomProvider compiler = CodeDomProvider.CreateProvider("CSharp");
            var results = compiler.CompileAssemblyFromDom(options, unit);
            if (IncludeCode)
            {
                using StreamWriter writer = new(String.Format("TMG.Modes.Generated.OptimizedMode{0}.cs", ModeName));
                //var results = compiler.CompileAssemblyFromDom( options, writer, unit );
                compiler.GenerateCodeFromCompileUnit(unit, writer, new CodeGeneratorOptions());
            }
            if (results.Errors.Count != 0)
            {
                error = results.Errors[0].ToString();
                optimizedMode = null;
                return false;
            }
            var assembly = results.CompiledAssembly;
            var theClass = assembly.GetType(String.Format("TMG.Modes.Generated.OptimizedMode{0}", ModeName));
            var constructor = theClass.GetConstructor(new Type[0]);
            optimizedMode = constructor?.Invoke(new object[0]) as IUtilityComponentMode;
            return true;
        }

        private bool CreateOptimizedMode(out IUtilityComponentMode optimizedMode, ref string error)
        {
            CodeCompileUnit unit = new();
            AddReferences(unit);
            CodeNamespace namespaceXTMF = AddNamespaces(unit);
            CodeTypeDeclaration modeClass = new(String.Format("OptimizedMode{0}", ModeName));
            modeClass.BaseTypes.Add(typeof(IUtilityComponentMode));
            modeClass.IsClass = true;
            modeClass.TypeAttributes = TypeAttributes.Public | TypeAttributes.Sealed;
            AttachBasicModeProperties(modeClass);
            AttachConstructor();
            AttachFeasible(modeClass);
            AttachRuntimeValidation(modeClass);
            AttachUtilityComponentInitializationMethod(modeClass);
            namespaceXTMF.Types.Add(modeClass);
            return CompileMode(unit, out optimizedMode, ref error);
        }

        private bool RemoveUsThenAddToParent(IUtilityComponentMode optimizedMode, ref string error)
        {
            var parentCat = Parent as IModeCategory;
            var parentRoot = Parent as I4StepModel;
            if (parentCat != null)
            {
                var ourLocation = parentCat.Children.IndexOf(this);
                parentCat.Children.Insert(ourLocation, optimizedMode);
                parentCat.Children.Remove(this);
            }
            else if (parentRoot != null)
            {
                var ourLocation = parentRoot.Modes.IndexOf(this);
                parentRoot.Modes.Insert(ourLocation, optimizedMode);
                parentRoot.Modes.Remove(this);
            }
            else
            {
                error = "In '" + Name + "' we were unable to work with a parent module of type '" + Parent.GetType().FullName + "'!";
                return false;
            }
            return true;
        }

        private void SetupUtilityComponentField(CodeTypeDeclaration modeClass, CodeMemberMethod initializeUtilityComponents, CodeMemberMethod calculateV, int utilityComponentIndex)
        {
            var utilityComponent = UtilityComponents[utilityComponentIndex];
            // Create the local field here
            var utilityVariableName = "_Generated_UtilityComponent" + utilityComponentIndex;
            var utilityVariable = new CodeMemberField(utilityComponent.GetType(), utilityVariableName)
            {
                Attributes = MemberAttributes.Private
            };
            modeClass.Members.Add(utilityVariable);
            // Initialize the local variable in the initializeUtilityComponents here
            initializeUtilityComponents.Statements.Add(new CodeAssignStatement(
                // LHS
                new CodeFieldReferenceExpression(new CodeThisReferenceExpression(), utilityVariableName),
                // RHS
                new CodeCastExpression(new CodeTypeReference(utilityComponent.GetType()),
                    new CodeIndexerExpression(new CodeArgumentReferenceExpression("utilityComponents"), new CodePrimitiveExpression(utilityComponentIndex)))
                ));
            // Add this to the CalculateV function
            calculateV.Statements.Add(
                new CodeAssignStatement(new CodeVariableReferenceExpression("v"),
                    new CodeBinaryOperatorExpression(new CodeVariableReferenceExpression("v"), CodeBinaryOperatorType.Add,
                        new CodeMethodInvokeExpression(
                            new CodeMethodReferenceExpression(new CodeFieldReferenceExpression(new CodeThisReferenceExpression(), utilityVariableName), "CalculateV"),
                            new CodeArgumentReferenceExpression("origin"), new CodeArgumentReferenceExpression("destination"), new CodeArgumentReferenceExpression("time"))))
                            );
        }
    }
}