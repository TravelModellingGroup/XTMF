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
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Datastructure;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace XTMF.Testing;

[TestClass]
public class TestGatewayLock
{
    [TestMethod]
    public void TestListWriter()
    {
        GatewayLock gate = new();
        for ( int iteration = 0; iteration < 100; iteration++ )
        {
            var list = new List<Entry>();
            Parallel.For( 0, 1000, i =>
                {
                    Random r = new();
                    for ( int j = 0; j < 100; j++ )
                    {
                        var num = r.Next( 10 );
                        bool found = false;
                        gate.PassThrough( () =>
                            {
                                foreach ( var entry in list )
                                {
                                    if ( entry.Number == num )
                                    {
                                        lock ( entry )
                                        {
                                            entry.TimeFound++;
                                            found = true;
                                            return;
                                        }
                                    }
                                }
                            } );
                        if ( !found )
                        {
                            gate.Lock( () =>
                            {
                                foreach ( var entry in list )
                                {
                                    if ( entry.Number == num )
                                    {
                                        entry.TimeFound++;
                                        found = true;
                                        return;
                                    }
                                }
                                list.Add( new Entry() { Number = num, TimeFound = 1 } );
                            } );
                        }
                    }
                } );
            Assert.IsTrue( list.Count <= 10 );
        }
    }

    [TestMethod]
    public void TestMultipleWriters()
    {
        GatewayLock gate = new();
        Stopwatch watch = new();
        watch.Start();
        long startLock1 = 0, startLock2 = 0;
        // ReSharper disable once NotAccessedVariable
        long endLock1 = 0, endLock2 = 0;
        for ( int i = 0; i < 100; i++ )
        {
            Parallel.Invoke(
                () =>
                {
                    gate.Lock(
                        () =>
                        {
                            startLock1 = watch.ElapsedTicks;
                            Thread.Sleep( 10 );
                            endLock1 = watch.ElapsedTicks;
                        } );
                },
                () =>
                {
                    gate.Lock(
                        () =>
                        {
                            startLock2 = watch.ElapsedTicks;
                            Thread.Sleep( 10 );
                            endLock2 = watch.ElapsedTicks;
                        } );
                } );
            if ( startLock1 < startLock2 )
            {
                Assert.IsTrue( endLock1 <= startLock2 );
            }
            else
            {
                Assert.IsTrue( endLock1 >= startLock2 );
            }
        }
    }

    [TestMethod]
    public void TestWriterHoldThenReaders()
    {
        GatewayLock gate = new();
        Stopwatch watch = new();
        watch.Start();
        bool writerDone = false;
        bool anyFails = false;
        Task main = Task.Factory.StartNew(
            () =>
            {
                var ourTasks = new Task[10];
                gate.Lock( () =>
                {
                    for ( int i = 0; i < ourTasks.Length; i++ )
                    {
                        ourTasks[i] = Task.Factory.StartNew(
                            () =>
                            {
                                gate.PassThrough( () =>
                                    {
                                        // ReSharper disable once AccessToModifiedClosure
                                        if ( !writerDone )
                                        {
                                            anyFails = true;
                                        }
                                    } );
                            } );
                    }
                    Thread.Sleep( 1000 );
                    writerDone = true;
                } );
                Task.WaitAll( ourTasks );
            } );
        main.Wait();
        Thread.MemoryBarrier();
        Assert.IsFalse( anyFails );
    }

    [TestMethod]
    public void TestWriterLock()
    {
        GatewayLock gate = new();
        Stopwatch watch = new();
        watch.Start();
        long finishedPassThrough = 0;
        long inLock = 0;
        Task secondaryTask = null;
        var mainTask = Task.Factory.StartNew(
            () =>
            {
                gate.PassThrough(
                    () =>
                    {
                        secondaryTask = Task.Factory.StartNew(
                            () =>
                            {
                                gate.Lock(
                                    () =>
                                    {
                                        // chill
                                        inLock = watch.ElapsedMilliseconds;
                                    } );
                            } );
                        Thread.Sleep( 100 );
                        finishedPassThrough = watch.ElapsedMilliseconds;
                    } );
            } );
        mainTask.Wait();
        secondaryTask.Wait();
        watch.Stop();
        Assert.IsTrue( inLock >= finishedPassThrough );
    }

    private class Entry
    {
        internal int Number;
        // Needed for testing concurrency 
        // ReSharper disable once NotAccessedField.Local
        internal int TimeFound;
    }
}