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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Datastructure;

namespace TMG.Emme
{
    public class Network : IDisposable
    {
        private static char[] SplitCharacters = new char[] { ',', ' ', '\t' };
        private SparseTwinIndex<Link> Links;
        private SparseArray<Node> Nodes;

        /// <summary>
        /// Create a blank network
        /// </summary>
        public Network()
        {
        }

        /// <summary>
        /// Create a new network from a 211 file
        /// </summary>
        /// <param name="fileName211">The 211 file to create the network from</param>
        public Network(string fileName211)
            : this()
        {
            LoadNetwork(fileName211);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool all)
        {
            Links = null;
            Nodes = null;
        }

        public bool GetData(out SparseArray<Node> nodes, out SparseTwinIndex<Link> links)
        {
            nodes = GetNodes();
            links = GetLinks();
            return true;
        }

        public SparseTwinIndex<Link> GetLinks()
        {
            return Links;
        }

        public SparseArray<Node> GetNodes()
        {
            return Nodes;
        }

        /// <summary>
        /// Load the network from a 211 file
        /// </summary>
        /// <param name="fileName211"></param>
        public void LoadNetwork(string fileName211)
        {
            Dictionary<int, Node> nodes = [];
            Dictionary<Pair<int, int>, Link> links = [];
            using (StreamReader reader = new StreamReader(fileName211))
            {
                string line;

                // run until we get to the link information
                while ((line = reader.ReadLine()) != null)
                {
                    string[] parts = line.Split(SplitCharacters, StringSplitOptions.RemoveEmptyEntries);
                    var numberOfParts = parts.Length;
                    if (numberOfParts >= 3 && parts[0].Length > 0)
                    {
                        int offset = -1;
                        if (parts[0][0] == 'a')
                        {
                            offset = 0;
                        }
                        else if (parts[0][0] == 'c')
                        {
                            continue;
                        }
                        Node node = new Node();
                        if (offset == -1)
                        {
                            node.IsCentroid = false;
                        }
                        else
                        {
                            node.IsCentroid = (parts[0].Length >= 2 && parts[0] == "a*");
                        }
                        node.Number = int.Parse(parts[1 + offset]);
                        node.X = float.Parse(parts[2 + offset]);
                        node.Y = float.Parse(parts[3 + offset]);
                        if (numberOfParts > 4 + offset)
                        {
                            node.User1 = int.Parse(parts[4 + offset]);
                            if (numberOfParts > 5 + offset)
                            {
                                node.User2 = int.Parse(parts[5 + offset]);
                                if (numberOfParts > 6 + offset)
                                {
                                    node.NodeType = int.Parse(parts[6 + offset]);
                                    node.Modified = false;
                                    if (parts.Length > 7 + offset)
                                    {
                                        node.NodeLabel = parts[7 + offset];
                                    }
                                    else
                                    {
                                        node.NodeLabel = node.Number.ToString();
                                    }
                                }
                            }
                        }
                        nodes.Add(node.Number, node);
                    }
                    if (line.StartsWith("t links"))
                    {
                        break;
                    }
                }

                while ((line = reader.ReadLine()) != null)
                {
                    string[] parts = line.Split(SplitCharacters, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length > 7)
                    {
                        var link = new Link(int.Parse(parts[1]), int.Parse(parts[2]))
                        {
                            Length = float.Parse(parts[3]),
                            Modes = parts[4].ToLower().ToCharArray(),
                            LinkType = int.Parse(parts[5]),
                            Lanes = float.Parse(parts[6]),
                            Vdf = float.Parse(parts[7]),
                            Speed = float.Parse(parts[9]),
                            Capacity = float.Parse(parts[10]),
                            Modified = false
                        };
                        // We don't load [8]
                        links.Add(new Pair<int, int>(link.I, link.J), link);
                    }
                }
            }

            // Now that we have loaded the data it is time to create the sparse structures
            var numberOfLinks = links.Count;
            var first = new int[numberOfLinks];
            var second = new int[numberOfLinks];
            var data = new Link[numberOfLinks];
            int i = 0;
            foreach (var l in links.Values)
            {
                first[i] = l.I;
                second[i] = l.J;
                data[i] = l;
                i++;
            }
            if (nodes.Values.Count == 0)
            {
                Nodes = null;
            }
            else
            {
                Nodes = SparseArray<Node>.CreateSparseArray((n => n.Number), nodes.Values.ToArray());
            }
            if (numberOfLinks == 0)
            {
                Links = null;
            }
            else
            {
                Links = SparseTwinIndex<Link>.CreateTwinIndex(first, second, data);
            }
        }

        public void SaveModFile(string fileName)
        {
            using (StreamWriter writer = new StreamWriter(fileName))
            {
                writer.WriteLine("t nodes");
                foreach (var nodeI in Nodes.ValidIndexies())
                {
                    var node = Nodes[nodeI];
                    if (node.Modified)
                    {
                        WriteNode(writer, node);
                    }
                }
                writer.WriteLine("t links");
                foreach (var linkI in Links.ValidIndexes())
                {
                    foreach (var linkJ in Links.ValidIndexes(linkI))
                    {
                        var link = Links[linkI, linkJ];
                        if (link.Modified)
                        {
                            WriteLink(writer, link);
                        }
                    }
                }
            }
        }

        public void UpdateLinks(Func<Link, bool> condition, Func<Link, Link> updateFunction)
        {
            var validFirstIndex = Links.ValidIndexes().ToArray();
            var length = validFirstIndex.Length;
            Parallel.For(0, length,
                delegate (int i)
                //for(int i = 0; i < length; i++)
                {
                    var vi = validFirstIndex[i];
                    var validSecondIndex = Links.ValidIndexes(vi).ToArray();
                    var lengthJ = validSecondIndex.Length;
                    for (int j = 0; j < lengthJ; j++)
                    {
                        var vj = validSecondIndex[j];
                        if (condition(Links[vi, vj]))
                        {
                            var updatedLink = updateFunction(Links[vi, vj]);
                            updatedLink.Modified = true;
                            Links[vi, vj] = updatedLink;
                        }
                    }
                });
        }

        private void WriteLink(StreamWriter writer, Link link)
        {
            writer.WriteLine("m {0} {1} {2} {3} {4} {5} {6} 0 {7} {8}",
                link.I, link.J, link.Length, new string(link.Modes), link.LinkType, link.Lanes, link.Vdf, link.Speed, link.Capacity);
        }

        // ReSharper disable UnusedParameter.Local
        private void WriteNode(StreamWriter writer, Node node)
        {
        }
    }
}