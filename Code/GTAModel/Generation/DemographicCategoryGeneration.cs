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
using Datastructure;
using XTMF;

namespace TMG.GTAModel
{
    public abstract class DemographicCategoryGeneration : IDemographicCategoryGeneration
    {
        [RunParameter( "Age Category Range", "0-8", typeof( RangeSet ), "The range of different age categories to use.  Age categories are defined in the Demographics module." )]
        public RangeSet AgeCategoryRange;

        [RunParameter( "Demographic Parameter Set Index", 0, "The 0 indexed index of parameters to use when calculating utility" )]
        public int DemographicParameterSetIndex;

        [RunParameter( "Employment Status", "2", typeof( RangeSet ), "Defaults: FullTime = 1, PartTime = 2" )]
        public RangeSet EmploymentStatusCategory;

        [RunParameter( "ModeChoice Parameter Set Index", 0, "The 0 indexed index of parameters to use when calculating utility" )]
        public int ModeChoiceParameterSetIndex;

        [RunParameter( "Work Type", "1", typeof( RangeSet ), "Defaults: 0 = Unemployed, 1 = professional, 2 = general, 3 = sales, 4 = manufacturing" )]
        public RangeSet OccupationCategory;

        [RootModule]
        public IDemographic4StepModelSystemTemplate Root;

        private SparseArray<Range> AgeCategories;

        private Range[] FlatAges;

        [RunParameter( "Mobility Category", "0", typeof( RangeSet ), "0= No Car and License, 1= car no license, 2= 2+ cars no license, 3= No Car with license, 4= license 1 car, 5= license 2+ cars" )]
        public RangeSet Mobility { get; set; }

        public string Name
        {
            get;
            set;
        }

        public float Progress
        {
            get;
            set;
        }

        public Tuple<byte, byte, byte> ProgressColour
        {
            get { return null; }
        }

        /// <summary>
        /// Create the production and attractions for this demographic
        /// </summary>
        /// <param name="production">The production</param>
        /// <param name="attractions">The attraction</param>
        public abstract void Generate(Datastructure.SparseArray<float> production, Datastructure.SparseArray<float> attractions);

        public virtual void InitializeDemographicCategory()
        {
            var dataBase = this.Root.ModeParameterDatabase;
            if ( dataBase != null )
            {
                dataBase.ApplyParameterSet( this.ModeChoiceParameterSetIndex, this.DemographicParameterSetIndex );
            }
        }

        public bool IsContained(IPerson person)
        {
            var age = person.Age;
            // Convert the age into an age category
            if ( !TryGetAgeCat( age, out age ) )
            {
                return false;
            }
            int mobilityCategory = 0;
            var cars = person.Household.Cars;
            mobilityCategory = cars + ( person.DriversLicense ? 3 : 0 );
            return ( this.EmploymentStatusCategory.Contains( person.EmploymentStatus ) ) & ( this.OccupationCategory.Contains( person.Occupation ) )
                        & ( this.AgeCategoryRange.Contains( age ) ) & ( this.Mobility.Contains( (int)mobilityCategory ) );
        }

        public virtual bool RuntimeValidation(ref string error)
        {
            return true;
        }

        protected bool TryGetAgeCat(int age, out int ageCat)
        {
            if ( this.AgeCategories == null )
            {
                var temp = this.Root.Demographics.AgeCategories;
                this.FlatAges = temp.GetFlatData();
                this.AgeCategories = temp;
            }
            for ( int i = 0; i < FlatAges.Length; i++ )
            {
                if ( this.FlatAges[i].ContainsInclusive( age ) )
                {
                    ageCat = this.AgeCategories.GetSparseIndex( i );
                    return true;
                }
            }
            ageCat = -1;
            return false;
        }
    }
}