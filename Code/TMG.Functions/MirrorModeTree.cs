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

namespace TMG.Functions;

public static class MirrorModeTree
{
    public static List<TreeData<T>> CreateMirroredTree<T>(List<IModeChoiceNode> modes)
    {
        List<TreeData<T>> ret = [];
        for ( int i = 0; i < modes.Count; i++ )
        {
            TreeData<T> node = new();
            if (modes[i] is IModeCategory asCat && asCat.Children != null)
            {
                node.Children = new TreeData<T>[asCat.Children.Count];
                for (int j = 0; j < node.Children.Length; j++)
                {
                    node.Children[j] = new TreeData<T>();
                    CreateMirroredTree(node.Children[j], asCat.Children[j]);
                }
            }
            ret.Add( node );
        }
        return ret;
    }

    private static void CreateMirroredTree<T>(TreeData<T> node, IModeChoiceNode mode)
    {
        if (mode is IModeCategory asCat && asCat.Children != null)
        {
            node.Children = new TreeData<T>[asCat.Children.Count];
            for (int j = 0; j < node.Children.Length; j++)
            {
                node.Children[j] = new TreeData<T>();
                CreateMirroredTree(node.Children[j], asCat.Children[j]);
            }
        }
    }

    public static TreeData<T> GetLeafNodeWithIndex<T>(List<TreeData<T>> list, int index)
    {
        int currentIndex = 0;
        for ( int i = 0; i < list.Count; i++ )
        {
            var ret = GetLeafNodeWithIndex( list[i], index, ref currentIndex );
            if ( ret != null )
            {
                return ret;
            }
        }
        return null;
    }

    private static TreeData<T> GetLeafNodeWithIndex<T>(TreeData<T> treeData, int index, ref int currentIndex)
    {
        if ( treeData.Children == null || treeData.Children.Length == 0 )
        {
            if ( index <= currentIndex )
            {
                return treeData;
            }
            currentIndex++;
        }
        else
        {
            for ( int i = 0; i < treeData.Children.Length; i++ )
            {
                var ret = GetLeafNodeWithIndex( treeData.Children[i], index, ref currentIndex );
                if ( ret != null )
                {
                    return ret;
                }
            }
        }
        return null;
    }
}