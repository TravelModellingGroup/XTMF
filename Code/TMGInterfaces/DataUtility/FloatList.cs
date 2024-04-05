/*
    Copyright 2014-2017 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using System.Collections.Generic;
using System.Text;

namespace TMG.DataUtility
{
    public sealed class FloatList : IList<float>
    {
        private float[] Values;

        public int Count => Values.Length;

        public bool IsReadOnly => false;

        public float this[int index]
        {
            get
            {
                return Values[index];
            }

            set
            {
                Values[index] = value;
            }
        }

        public static bool TryParse(string input, out FloatList data)
        {
            string error = null;
            return TryParse( ref error, input, out data );
        }

        public static bool TryParse(ref string error, string input, out FloatList data)
        {
            data = null;
            List<float> values = [];
            int i = 0;
            BurnWhiteSpace( ref i, input );
            var length = input.Length;
            while ( i < length )
            {
                float p;
                char c = input[i];
                bool exponential = false;
                bool negative = false;
                bool negativeExponential = false;
                // Read in the Data
                int pastDecimal = -1;
                int exponent = 0;
                p = 0;
                do
                {
                    if ( exponential )
                    {
                        if ( c == '-' )
                        {
                            negativeExponential = true;
                        }
                        else if ( c == '+' )
                        {
                            // do nothing
                        }
                        else if ( ( c < '0' | c > '9' ) )
                        {
                            error = "We found a " + c + " while trying to read in the data!";
                            return false;
                        }
                        else
                        {
                            exponent = exponent * 10 + ( c - '0' );
                        }
                    }
                    else
                    {
                        if ( ( c == '.' ) )
                        {
                            pastDecimal = 0;
                        }
                        else
                        {
                            if ( c == 'e' | c == 'E' )
                            {
                                exponential = true;
                            }
                            else if ( c == '-' )
                            {
                                negative = true;
                            }
                            else if ( ( c < '0' | c > '9' ) )
                            {
                                error = "We found a " + c + " while trying to read in the data!";
                                return false;
                            }
                            else
                            {
                                p = p * 10 + ( c - '0' );
                                if ( pastDecimal >= 0 )
                                {
                                    pastDecimal++;
                                }
                            }
                        }
                    }
                } while ( ++i < length && ( ( c = input[i] ) != '\t' & c != '\n' & c != '\r' & c != ' ' & c != ',' ) );
                if ( negativeExponential )
                {
                    exponent = -exponent;
                }
                if ( pastDecimal > 0 )
                {
                    p = p * (float)Math.Pow( 0.1, pastDecimal - exponent );
                }
                if ( negative )
                {
                    p = -p;
                }
                BurnWhiteSpace( ref i, input );
                values.Add( p );
            }
            data = [];
            data.Values = values.ToArray();
            return true;
        }

        public void Add(float item)
        {
            throw new NotSupportedException();
        }

        public void Clear()
        {
            throw new NotSupportedException();
        }

        public bool Contains(float item)
        {
            return IndexOf( item ) != -1;
        }

        public void CopyTo(float[] array, int arrayIndex)
        {
            Array.Copy( Values, 0, array, arrayIndex, Values.Length );
        }

        public IEnumerator<float> GetEnumerator()
        {
            for ( int i = 0; i < Values.Length; i++ )
            {
                yield return Values[i];
            }
        }

        public int IndexOf(float item)
        {
            for ( int i = 0; i < Values.Length; i++ )
            {
                // ReSharper disable once CompareOfFloatsByEqualityOperator
                if ( item == Values[i] )
                {
                    return i;
                }
            }
            return -1;
        }

        public void Insert(int index, float item)
        {
            throw new NotSupportedException();
        }

        public bool Remove(float item)
        {
            throw new NotSupportedException();
        }

        public void RemoveAt(int index)
        {
            throw new NotSupportedException();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return Values.GetEnumerator();
        }

        public override string ToString()
        {
            StringBuilder builder = new StringBuilder();
            for ( int i = 0; i < Values.Length; i++ )
            {
                builder.Append( Values[i] );
                builder.Append( ',' );
            }
            return builder.ToString( 0, builder.Length - 1 );
        }

        private static void BurnWhiteSpace(ref int i, string input)
        {
            while ( i < input.Length && WhiteSpace( input[i] ) )
            {
                i++;
            }
        }

        private static bool WhiteSpace(char p)
        {
            switch ( p )
            {
                case '\t':
                case ' ':
                case ',':
                    return true;

                default:
                    return false;
            }
        }
    }
}