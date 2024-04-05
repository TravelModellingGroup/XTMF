using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using XTMF.Networking;
// ReSharper disable AccessToDisposedClosure
namespace XTMF.Testing;

[TestClass]
public class TestMessageQueue
{
    [TestMethod]
    public void TestBasicMessageQueue()
    {
        using var queue = new MessageQueue<int>();
        var length = 100;
        for (int i = 0; i < length; i++)
        {
            queue.Add(i);
        }
        for (int i = 0; i < length; i++)
        {
            Assert.AreEqual(i, queue.GetMessage());
        }
        Assert.AreEqual(0, queue.Count);
    }

    [TestMethod]
    public void TestThreadedMessageQueue()
    {
        using var queue = new MessageQueue<int>();
        var length = 10000;
        bool[] found = new bool[length];
        Parallel.Invoke(
            () =>
        {
            for (int i = 0; i < length; i++)
            {
                found[i] = false;
                queue.Add(i + 1);
            }
        },
        () =>
        {
            int res;
            while ((res = queue.GetMessageOrTimeout(10)) > 0)
            {
                found[res - 1] = true;
            }
        },
        () =>
        {
            int res;
            while ((res = queue.GetMessageOrTimeout(10)) > 0)
            {
                found[res - 1] = true;
            }
        });
        for (int i = 0; i < length; i++)
        {
            Assert.AreEqual(true, found[i]);
        }
        Assert.AreEqual(0, queue.Count);
    }
}
