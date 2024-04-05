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
namespace Tasha.Common;

/// <summary>
/// Describes what type of job a person does
///
/// G General Office/Clerical
/// M Manufacturing/Construction/Trades
/// P Professional/Management/Technical
/// S Retail Sales and Service
/// O Not Employed
/// 9 Refused/Unknown
///
/// </summary>
public enum Occupation
{
    /// <summary>
    ///
    /// </summary>
    Office = 'G',

    /// <summary>
    ///
    /// </summary>
    Manufacturing = 'M',

    /// <summary>
    ///
    /// </summary>
    Professional = 'P',

    /// <summary>
    ///
    /// </summary>
    Retail = 'S',

    /// <summary>
    ///
    /// </summary>
    Farmer = 'F',

    /// <summary>
    ///
    /// </summary>
    NotEmployed = 'O',

    /// <summary>
    ///
    /// </summary>
    Unknown = '9'
}