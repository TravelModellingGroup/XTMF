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
using Datastructure;
using XTMF;

namespace TMG;

/// <summary>
/// This interface provides a basic set of Demographic needs to supplement the responsibilities of the IZoneLoader
/// </summary>
public interface IDemographicsData : IDataSource<IDemographicsData>
{
    /// <summary>
    /// The definition of the age categories used
    /// </summary>
    SparseArray<Range> AgeCategories { get; }

    /// <summary>
    /// AgeRate [Zone,AgeCategory]
    /// </summary>
    SparseTwinIndex<float> AgeRates { get; }

    /// <summary>
    /// EmploymentRate [Zone][AgeCategory, EmploymentCategory]
    /// </summary>
    SparseArray<SparseTwinIndex<float>> DriversLicenseRates { get; }

    /// <summary>
    /// The definition of the employment status used
    /// </summary>
    SparseArray<string> EmploymentStatus { get; }

    /// <summary>
    /// EmploymentRate [Zone][AgeCategory, EmploymentCategory]
    /// </summary>
    SparseArray<SparseTwinIndex<float>> EmploymentStatusRates { get; }

    /// <summary>
    /// [Zone,EmploymentStatus,OccupationType]
    /// </summary>
    SparseTriIndex<float> JobOccupationRates { get; }

    /// <summary>
    /// [Zone, Job Type (unemployed, full-time, part-time)]
    /// </summary>
    SparseTwinIndex<float> JobTypeRates { get; }

    /// <summary>
    /// [Zone][Driver's License, AgeCategory, Number of Vehicles]
    /// </summary>
    SparseArray<SparseTriIndex<float>> NonWorkerVehicleRates { get; }

    /// <summary>
    /// The definition of the occupations used
    /// </summary>
    SparseArray<string> OccupationCategories { get; }

    /// <summary>
    /// OccupationRate [AgeCategory, EmploymentStatus, OccupationType]
    /// </summary>
    SparseArray<SparseTriIndex<float>> OccupationRates { get; }

    /// <summary>
    /// SchoolRate [Zone][AgeCategory, EmploymentCategory]
    /// </summary>
    SparseArray<SparseTwinIndex<float>> SchoolRates { get; }

    /// <summary>
    /// [Zone][Driver's License, Occupation, Number Of Vehicles]
    /// </summary>
    SparseArray<SparseTriIndex<float>> WorkerVehicleRates { get; }
}