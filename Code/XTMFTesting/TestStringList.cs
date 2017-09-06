using Microsoft.VisualStudio.TestTools.UnitTesting;
using TMG.DataUtility;
namespace XTMF.Testing
{
    [TestClass]
    public class TestStringList
    {
        [TestMethod]
        public void TestStringListTryParse()
        {
            string error = null;
            if (!StringList.TryParse(ref error, "Hello World,Second,With \\, Comma", out StringList list))
            {
                Assert.Fail("StringList should never return false!");
            }
            Assert.AreEqual( 3, list.Count );
            Assert.AreEqual( "Hello World", list[0] );
            Assert.AreEqual( "Second", list[1] );
            Assert.AreEqual( "With , Comma", list[2] );
        }

        [TestMethod]
        public void TestJoin()
        {
            string error = null;
            if (!StringList.TryParse(ref error, "Hello World,Second,With \\, Comma", out StringList list))
            {
                Assert.Fail("StringList should never return false!");
            }
            Assert.AreEqual( 3, list.Count );
            var result = string.Join( ",", list.ToArray() );
            Assert.AreEqual( "Hello World,Second,With , Comma", result );
        }
        [TestMethod]
        public void TestToString()
        {
            string error = null;
            if (!StringList.TryParse(ref error, "Hello World,Second,With \\, Comma", out StringList list))
            {
                Assert.Fail("StringList should never return false!");
            }
            Assert.AreEqual( 3, list.Count );
            var result = list.ToString();
            Assert.AreEqual( "Hello World,Second,With \\, Comma", result );
        }

    }
}
