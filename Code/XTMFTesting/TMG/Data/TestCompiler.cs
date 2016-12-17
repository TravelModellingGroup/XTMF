/*
    Copyright 2016 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using TMG;
using TMG.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Datastructure;
using TMG.Frameworks.Data.Processing.AST;

namespace XTMF.Testing.TMG.Data
{
    [TestClass]
    public class TestCompiler
    {
        /// <summary>
        /// Create a new simple matrix for testing.
        /// </summary>
        /// <param name="m11"></param>
        /// <param name="m12"></param>
        /// <param name="m21"></param>
        /// <param name="m22"></param>
        /// <returns></returns>
        private IDataSource<SparseTwinIndex<float>> CreateData(string name, float m11, float m12, float m21, float m22)
        {
            SparseIndexing indexes = new SparseIndexing();
            indexes.Indexes = new SparseSet[]
            {
                new SparseSet()
                {
                    Start = 1,
                    Stop = 2,
                    SubIndex = new SparseIndexing()
                    {
                        Indexes = new SparseSet[] { new SparseSet() { Start = 1, Stop = 2  } }
                    }
                }
            };

            float[][] data = new float[][] { new float[] { m11, m12 }, new float[] { m21, m22 } };
            return new MatrixSource(new SparseTwinIndex<float>(indexes, data)) { Name = name };
        }

        /// <summary>
        /// Createa a new simple vector for testing.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="m1"></param>
        /// <param name="m2"></param>
        /// <returns></returns>
        private IDataSource<SparseArray<float>> CreateData(string name, float m1, float m2)
        {
            SparseIndexing indexes = new SparseIndexing();
            indexes.Indexes = new SparseSet[]
            {
                new SparseSet()
                {
                    Start = 1,
                    Stop = 2,
                }
            };
            float[] data = new float[] { m1, m2 };
            return new VectorSource(new SparseArray<float>(indexes, data)) { Name = name };
        }

        [TestMethod]
        public void TestMatrixAdd()
        {
            CompareMatrix("A + B", new IDataSource[]
            {
                CreateData("A", 1, 2, 3, 4),
                CreateData("B", 2, 4, 6, 8)
            }, 3.0f, 6.0f, 9.0f, 12.0f);
        }

        [TestMethod]
        public void TestMatrixSubtract()
        {
            CompareMatrix("A - B", new IDataSource[]
            {
                CreateData("A", 1, 2, 3, 4),
                CreateData("B", 2, 4, 6, 8)
            }, -1.0f, -2.0f, -3.0f, -4.0f);
        }

        [TestMethod]
        public void TestMatrixVectorSubtract()
        {
            CompareMatrix("A - SumColumns(B)", new IDataSource[]
            {
                CreateData("A", 1, 2, 3, 4),
                CreateData("B", 2, 4, 6, 8)
            }, -7.0f, -10.0f, -5.0f, -8.0f);
        }

        [TestMethod]
        public void TestMatrixVectorSubtract2()
        {
            CompareMatrix("A - SumRows(B)", new IDataSource[]
            {
                CreateData("A", 1, 2, 3, 4),
                CreateData("B", 2, 4, 6, 8)
            }, -5.0f, -4.0f, -11.0f, -10.0f);
        }

        [TestMethod]
        public void TestMatrixAddWithBrackets()
        {
            CompareMatrix("(A) + (B)", new IDataSource[]
            {
                CreateData("A", 1, 2, 3, 4),
                CreateData("B", 2, 4, 6, 8)
            }, 3.0f, 6.0f, 9.0f, 12.0f);
        }

        [TestMethod]
        public void TestMatrixAddWithDoubleBrackets()
        {
            CompareMatrix("((A)) + ((B))", new IDataSource[]
            {
                CreateData("A", 1, 2, 3, 4),
                CreateData("B", 2, 4, 6, 8)
            }, 3.0f, 6.0f, 9.0f, 12.0f);
        }

        [TestMethod]
        public void TestMatrixAddWithTripleBrackets()
        {
            CompareMatrix("(((A)) + ((B)))", new IDataSource[]
            {
                CreateData("A", 1, 2, 3, 4),
                CreateData("B", 2, 4, 6, 8)
            }, 3.0f, 6.0f, 9.0f, 12.0f);
        }

        [TestMethod]
        public void TestMatrixSumRows()
        {
            string error = null;
            Expression ex;
            Assert.IsTrue(Compiler.Compile("SumRows(A + B)", out ex, ref error));
            var result = ex.Evaluate(new IDataSource[]
            {
                CreateData("A", 1, 2, 3, 4),
                CreateData("B", 2, 4, 6, 8)
            });
            Assert.IsTrue(result.IsVectorResult);
            Assert.IsTrue(result.Direction == ComputationResult.VectorDirection.Vertical);
            var flat = result.VectorData.GetFlatData();
            Assert.AreEqual(9.0f, flat[0], 0.00001f);
            Assert.AreEqual(21.0f, flat[1], 0.00001f);
        }

        [TestMethod]
        public void TestMatrixSumColumns()
        {
            string error = null;
            Expression ex;
            Assert.IsTrue(Compiler.Compile("SumColumns(A + B)", out ex, ref error));
            var result = ex.Evaluate(new IDataSource[]
            {
                CreateData("A", 1, 2, 3, 4),
                CreateData("B", 2, 4, 6, 8)
            });
            Assert.IsTrue(result.IsVectorResult);
            Assert.IsTrue(result.Direction == ComputationResult.VectorDirection.Horizontal);
            var flat = result.VectorData.GetFlatData();
            Assert.AreEqual(12.0f, flat[0], 0.00001f);
            Assert.AreEqual(18.0f, flat[1], 0.00001f);
        }

        [TestMethod]
        public void TestMatrixAsHorizontal()
        {
            string error = null;
            Expression ex;
            Assert.IsTrue(Compiler.Compile("AsHorizontal(SumRows(A + B))", out ex, ref error));
            var result = ex.Evaluate(new IDataSource[]
            {
                CreateData("A", 1, 2, 3, 4),
                CreateData("B", 2, 4, 6, 8)
            });
            Assert.IsTrue(result.IsVectorResult);
            Assert.IsTrue(result.Direction == ComputationResult.VectorDirection.Horizontal);
            var flat = result.VectorData.GetFlatData();
            Assert.AreEqual(9.0f, flat[0], 0.00001f);
            Assert.AreEqual(21.0f, flat[1], 0.00001f);
        }

        [TestMethod]
        public void TestMatrixSum()
        {
            string error = null;
            Expression ex;
            Assert.IsTrue(Compiler.Compile("Sum(A + B)", out ex, ref error));
            var result = ex.Evaluate(new IDataSource[]
            {
                CreateData("A", 1, 2, 3, 4),
                CreateData("B", 2, 4, 6, 8)
            });
            Assert.IsTrue(result.IsValue);
            Assert.AreEqual(30.0f, result.LiteralValue, 0.00001f);
        }

        [TestMethod]
        public void TestVectorSum()
        {
            string error = null;
            Expression ex;
            Assert.IsTrue(Compiler.Compile("Sum(SumRows(A + B))", out ex, ref error));
            var result = ex.Evaluate(new IDataSource[]
            {
                CreateData("A", 1, 2, 3, 4),
                CreateData("B", 2, 4, 6, 8)
            });
            Assert.IsTrue(result.IsValue);
            Assert.AreEqual(30.0f, result.LiteralValue, 0.00001f);
        }

        [TestMethod]
        public void TestMatrixTranspose()
        {
            CompareMatrix("Transpose(A + B)", new IDataSource[]
            {
                CreateData("A", 1, 2, 3, 4),
                CreateData("B", 2, 4, 6, 8)
            }, 3.0f, 9.0f, 6.0f, 12.0f);
        }

        [TestMethod]
        public void TestMatrixAbs()
        {
            CompareMatrix("Abs(A - B)", new IDataSource[]
            {
                CreateData("A", 1, 2, 3, 4),
                CreateData("B", 2, 4, 6, 8)
            }, 1.0f, 2.0f, 3.0f, 4.0f);
        }

        [TestMethod]
        public void TestVectorAbs()
        {
            string error = null;
            Expression ex;
            Assert.IsTrue(Compiler.Compile("Abs(SumRows(A) - SumRows(B))", out ex, ref error));
            var result = ex.Evaluate(new IDataSource[]
            {
                CreateData("A", 1, 2, 3, 4),
                CreateData("B", 2, 4, 6, 8)
            });
            Assert.IsTrue(result.IsVectorResult);
            var flat = result.VectorData.GetFlatData();
            Assert.AreEqual(3.0f, flat[0], 0.00001f);
            Assert.AreEqual(7.0f, flat[1], 0.00001f);
        }

        [TestMethod]
        public void TestScalarAbs()
        {
            string error = null;
            Expression ex;
            Assert.IsTrue(Compiler.Compile("Abs(Sum(A) - Sum(B))", out ex, ref error));
            var result = ex.Evaluate(new IDataSource[]
            {
                CreateData("A", 1, 2, 3, 4),
                CreateData("B", 2, 4, 6, 8)
            });
            Assert.IsTrue(result.IsValue);
            Assert.AreEqual(10.0f, result.LiteralValue, 0.00001f);
        }

        [TestMethod]
        public void TestMatrixAvg()
        {
            string error = null;
            Expression ex;
            Assert.IsTrue(Compiler.Compile("Avg(A - B)", out ex, ref error));
            var result = ex.Evaluate(new IDataSource[]
            {
                CreateData("A", 1, 2, 3, 4),
                CreateData("B", 2, 4, 6, 8)
            });
            Assert.IsTrue(result.IsValue);
            Assert.AreEqual(-2.5f, result.LiteralValue, 0.00001f);
        }

        [TestMethod]
        public void TestVectorAvg()
        {
            string error = null;
            Expression ex;
            Assert.IsTrue(Compiler.Compile("Avg(SumRows(A) - SumRows(B))", out ex, ref error));
            var result = ex.Evaluate(new IDataSource[]
            {
                CreateData("A", 1, 2, 3, 4),
                CreateData("B", 2, 4, 6, 8)
            });
            Assert.IsTrue(result.IsValue);
            Assert.AreEqual(-5.0f, result.LiteralValue, 0.00001f);
        }

        [TestMethod]
        public void TestMatrixAvgRows()
        {
            string error = null;
            Expression ex;
            Assert.IsTrue(Compiler.Compile("AvgRows(A)", out ex, ref error));
            var result = ex.Evaluate(new IDataSource[]
            {
                CreateData("A", 1, 2, 3, 4),
                CreateData("B", 2, 4, 6, 8)
            });
            Assert.IsTrue(result.IsVectorResult);
            Assert.IsTrue(result.Direction == ComputationResult.VectorDirection.Vertical);
            var flat = result.VectorData.GetFlatData();
            Assert.AreEqual(1.5f, flat[0], 0.00001f);
            Assert.AreEqual(3.5f, flat[1], 0.00001f);
        }

        [TestMethod]
        public void TestMatrixAvgColumns()
        {
            string error = null;
            Expression ex;
            Assert.IsTrue(Compiler.Compile("AvgColumns(A)", out ex, ref error));
            var result = ex.Evaluate(new IDataSource[]
            {
                CreateData("A", 1, 2, 3, 4),
                CreateData("B", 2, 4, 6, 8)
            });
            Assert.IsTrue(result.IsVectorResult);
            Assert.IsTrue(result.Direction == ComputationResult.VectorDirection.Horizontal);
            var flat = result.VectorData.GetFlatData();
            Assert.AreEqual(2.0f, flat[0], 0.00001f);
            Assert.AreEqual(3.0f, flat[1], 0.00001f);
        }

        [TestMethod]
        public void TestPI()
        {
            string error = null;
            Expression ex;
            Assert.IsTrue(Compiler.Compile("PI( )", out ex, ref error));
            var result = ex.Evaluate(new IDataSource[0]);
            Assert.IsTrue(result.IsValue);
            Assert.AreEqual((float)Math.PI, result.LiteralValue, 0.000001f);
        }

        [TestMethod]
        public void TestE()
        {
            string error = null;
            Expression ex;
            Assert.IsTrue(Compiler.Compile("E( )", out ex, ref error));
            var result = ex.Evaluate(new IDataSource[0]);
            Assert.IsTrue(result.IsValue);
            Assert.AreEqual((float)Math.E, result.LiteralValue, 0.000001f);
        }

        [TestMethod]
        public void TestMatrixExponent()
        {
            CompareMatrix("A ^ B", new IDataSource[]
            {
                CreateData("A", 1, 2, 3, 4),
                CreateData("B", 2, 4, 6, 8)
            }, 1.0f, 16.0f, 729.0f, 65536.0f);
        }

        [TestMethod]
        public void TestMatrixExponent2()
        {
            CompareMatrix("(A + 1) ^ (B - 1)", new IDataSource[]
            {
                CreateData("A", 1, 2, 3, 4),
                CreateData("B", 2, 4, 6, 8)
            }, 2.0f, 27.0f, 1024.0f, 78125.0f);
        }

        [TestMethod]
        public void TestMatrixEqual()
        {
            CompareMatrix("A == B", new IDataSource[]
            {
                CreateData("A", 1, 2, 3, 4),
                CreateData("B", 1, 2, 4, 3)
            }, 1.0f, 1.0f, 0.0f, 0.0f);
        }

        [TestMethod]
        public void TestMatrixNotEqual()
        {
            CompareMatrix("A != B", new IDataSource[]
            {
                CreateData("A", 1, 2, 3, 4),
                CreateData("B", 1, 2, 4, 3)
            }, 0.0f, 0.0f, 1.0f, 1.0f);
        }

        [TestMethod]
        public void TestMatrixLessThan()
        {
            CompareMatrix("A < B", new IDataSource[]
            {
                CreateData("A", 1, 2, 3, 4),
                CreateData("B", 1, 2, 4, 3)
            }, 0.0f, 0.0f, 1.0f, 0.0f);
        }

        [TestMethod]
        public void TestMatrixGreaterThan()
        {
            CompareMatrix("A > B", new IDataSource[]
            {
                CreateData("A", 1, 2, 3, 4),
                CreateData("B", 1, 2, 4, 3)
            }, 0.0f, 0.0f, 0.0f, 1.0f);
        }

        [TestMethod]
        public void TestMatrixLessThanOrEqual()
        {
            CompareMatrix("A <= B", new IDataSource[]
            {
                CreateData("A", 1, 2, 3, 4),
                CreateData("B", 1, 2, 4, 3)
            }, 1.0f, 1.0f, 1.0f, 0.0f);
        }

        [TestMethod]
        public void TestMatrixGreaterThanOrEqual()
        {
            CompareMatrix("A >= B", new IDataSource[]
            {
                CreateData("A", 1, 2, 3, 4),
                CreateData("B", 1, 2, 4, 3)
            }, 1.0f, 1.0f, 0.0f, 1.0f);
        }

        [TestMethod]
        public void TestMatrixCompareAdvanced()
        {
            CompareMatrix("(A == B) + (A != B)", new IDataSource[]
            {
                CreateData("A", 1, 2, 3, 4),
                CreateData("B", 1, 2, 4, 3)
            }, 1.0f, 1.0f, 1.0f, 1.0f);
        }

        [TestMethod]
        public void TestAnd()
        {
            CompareMatrix("(A == B) & (A != B)", new IDataSource[]
            {
                CreateData("A", 1, 2, 3, 4),
                CreateData("B", 1, 2, 4, 3)
            }, 0.0f, 0.0f, 0.0f, 0.0f);
        }

        [TestMethod]
        public void TestOr()
        {
            CompareMatrix("(A == B) | (A != B)", new IDataSource[]
            {
                CreateData("A", 1, 2, 3, 4),
                CreateData("B", 1, 2, 4, 3)
            }, 1.0f, 1.0f, 1.0f, 1.0f);
        }

        [TestMethod]
        public void TestMatrixLength()
        {
            string error = null;
            Expression ex;
            Assert.IsTrue(Compiler.Compile("Length(A + 1) + Length(B - 1)", out ex, ref error));
            var result = ex.Evaluate(new IDataSource[]
            {
                CreateData("A", 1, 2, 3, 4),
                CreateData("B", 2, 4, 6, 8)
            });
            Assert.IsTrue(result.IsValue);
            Assert.AreEqual(8.0f, result.LiteralValue, 0.00001f);
        }

        [TestMethod]
        public void TestMatrix()
        {
            CompareMatrix("Matrix(asHorizontal(E)) + Matrix(asVertical(E))", new IDataSource[]
            {
                CreateData("E", 9, 10)
            }, 18.0f, 19.0f, 19.0f, 20.0f);
        }

        [TestMethod]
        public void TestIdentity()
        {
            var data = new IDataSource[]
            {
                CreateData("A", 1, 2, 3, 4),
                CreateData("E", 9, 10)
            };
            CompareMatrix("identityMatrix(E)", data, 1.0f, 0.0f, 0.0f, 1.0f);
            CompareMatrix("identityMatrix(A)", data, 1.0f, 0.0f, 0.0f, 1.0f);
        }

        [TestMethod]
        public void TestOptimizeFusedMultiplyAddIsOptimizedIn()
        {
            string error = null;
            Expression ex;
            Assert.IsTrue(Compiler.Compile("A * B + A", out ex, ref error), $"Unable to compile 'A * B + A'\r\n{error}");
            Assert.IsInstanceOfType(ex, typeof(FusedMultiplyAdd));
            Assert.IsTrue(Compiler.Compile("A + B * A", out ex, ref error), $"Unable to compile 'A * B + A'\r\n{error}");
            Assert.IsInstanceOfType(ex, typeof(FusedMultiplyAdd));
            Assert.IsTrue(Compiler.Compile("A * B + 4.0 * A + B * 1.2", out ex, ref error), $"Unable to compile 'A * B + A'\r\n{error}");
            Assert.IsInstanceOfType(ex, typeof(FusedMultiplyAdd));
        }

        [TestMethod]
        public void TestOptimizeFusedMultiplyAdd()
        {
            CompareMatrix("A * B + A", new IDataSource[]
            {
                CreateData("A", 1, 2, 3, 4),
                CreateData("B", 1, 2, 4, 3)
            }, 2.0f, 6.0f, 15.0f, 16.0f);
        }

        [TestMethod]
        public void TestOptimizeAddLiterals()
        {
            string error = null;
            Expression ex;
            Assert.IsTrue(Compiler.Compile("1 + 2", out ex, ref error), $"Unable to compile '1 + 2'\r\n{error}");
            var result = ex.Evaluate(new IDataSource[]
            {
                CreateData("A", 1, 2, 3, 4),
                CreateData("B", 1, 2, 4, 3)
            });
            Assert.IsTrue(result.IsValue);
            Assert.AreEqual(3.0f, result.LiteralValue);
            Assert.IsInstanceOfType(ex, typeof(Literal));
            Assert.AreEqual(3.0f, ((Literal)ex).Value);
        }

        [TestMethod]
        public void TestOptimizeSubtractLiterals()
        {
            string error = null;
            Expression ex;
            Assert.IsTrue(Compiler.Compile("1 - 2", out ex, ref error), $"Unable to compile '1 - 2'\r\n{error}");
            var result = ex.Evaluate(new IDataSource[]
            {
                CreateData("A", 1, 2, 3, 4),
                CreateData("B", 1, 2, 4, 3)
            });
            Assert.IsTrue(result.IsValue);
            Assert.AreEqual(-1.0f, result.LiteralValue);
            Assert.IsInstanceOfType(ex, typeof(Literal));
            Assert.AreEqual(-1.0f, ((Literal)ex).Value);
        }

        [TestMethod]
        public void TestOptimizeMultiplyLiterals()
        {
            string error = null;
            Expression ex;
            Assert.IsTrue(Compiler.Compile("1 * 2", out ex, ref error), $"Unable to compile '1 * 2'\r\n{error}");
            var result = ex.Evaluate(new IDataSource[]
            {
                CreateData("A", 1, 2, 3, 4),
                CreateData("B", 1, 2, 4, 3)
            });
            Assert.IsTrue(result.IsValue);
            Assert.AreEqual(2.0f, result.LiteralValue);
            Assert.IsInstanceOfType(ex, typeof(Literal));
            Assert.AreEqual(2.0f, ((Literal)ex).Value);
        }

        [TestMethod]
        public void TestOptimizeDivideLiterals()
        {
            string error = null;
            Expression ex;
            Assert.IsTrue(Compiler.Compile("1 / 2", out ex, ref error), $"Unable to compile '1 / 2'\r\n{error}");
            var result = ex.Evaluate(new IDataSource[]
            {
                CreateData("A", 1, 2, 3, 4),
                CreateData("B", 1, 2, 4, 3)
            });
            Assert.IsTrue(result.IsValue);
            Assert.AreEqual(0.5f, result.LiteralValue);
            Assert.IsInstanceOfType(ex, typeof(Literal));
            Assert.AreEqual(0.5f, ((Literal)ex).Value);
        }

        [TestMethod]
        public void TestOptimizeDivideToMultiply()
        {
            var data = new IDataSource[]
            {
                CreateData("A", 1, 2, 3, 4),
                CreateData("B", 1, 2, 4, 3)
            };
            Assert.IsInstanceOfType(CompareMatrix("A / 2", data, 0.5f, 1.0f, 1.5f, 2.0f), typeof(Multiply));
        }

        /// <summary>
        /// Assert results
        /// </summary>
        private static Expression CompareMatrix(string equation, IDataSource[] data, float m11, float m12, float m21, float m22)
        {
            string error = null;
            Expression ex;
            Assert.IsTrue(Compiler.Compile(equation, out ex, ref error), $"Unable to compile '{equation}'\r\n{error}");
            var result = ex.Evaluate(data);
            Assert.IsTrue(result.IsODResult);
            var flat = result.ODData.GetFlatData();
            Assert.AreEqual(m11, flat[0][0], 0.00001f);
            Assert.AreEqual(m12, flat[0][1], 0.00001f);
            Assert.AreEqual(m21, flat[1][0], 0.00001f);
            Assert.AreEqual(m22, flat[1][1], 0.00001f);
            return ex;
        }

        class VectorSource : IDataSource<SparseArray<float>>
        {
            public bool Loaded { get; set; }


            public string Name { get; set; }


            public float Progress { get; set; }

            SparseArray<float> Data;

            public VectorSource(SparseArray<float> vector)
            {
                Data = vector;
                Loaded = true;
            }


            public Tuple<byte, byte, byte> ProgressColour
            {
                get
                {
                    return null;
                }
            }

            public SparseArray<float> GiveData()
            {
                return Data;
            }

            public void LoadData()
            {

            }

            public bool RuntimeValidation(ref string error)
            {
                return true;
            }

            public void UnloadData()
            {

            }
        }

        class MatrixSource : IDataSource<SparseTwinIndex<float>>
        {
            public bool Loaded { get; set; }


            public string Name { get; set; }


            public float Progress { get; set; }

            SparseTwinIndex<float> Data;

            public MatrixSource(SparseTwinIndex<float> matrix)
            {
                Data = matrix;
                Loaded = true;
            }


            public Tuple<byte, byte, byte> ProgressColour
            {
                get
                {
                    return null;
                }
            }

            public SparseTwinIndex<float> GiveData()
            {
                return Data;
            }

            public void LoadData()
            {

            }

            public bool RuntimeValidation(ref string error)
            {
                return true;
            }

            public void UnloadData()
            {

            }
        }
    }
}
