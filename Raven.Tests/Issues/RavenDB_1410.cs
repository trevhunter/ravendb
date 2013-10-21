using System.Collections.Generic;
using System.Linq;
using Raven.Client;
using Raven.Client.Indexes;
using Xunit;
using Raven.Client.Linq;
using System;
using Raven.Abstractions.Exceptions;

namespace Raven.Tests.Issues
{
    // Relating to http://issues.hibernatingrhinos.com/issue/RavenDB-1410

    public class RavenDB_1410 : RavenTest
    {

        [Fact]
        public void Stream_Should_Throw_Descriptive_Exception_If_No_Index_Specified()
        {
            // Arrange
            var testObj = new TestDataObject() { Name = "Test1" };
            var testObj1 = new TestDataObject() { Name = "Test2" };

            using (var docStore = NewDocumentStore())
            using (var session = docStore.OpenSession())
            {
                InsertTestData(session, testObj, testObj1);

                // Act
                var query = session.Query<TestDataObject>();

                // Assert: (1) Underlying query doesn't throw and (2) Stream throws a descriptive error
                Assert.DoesNotThrow(() =>
                {
                    var list = query.ToList();
                });

                var streamEnumerator = session.Advanced.Stream(query);

                // In Raven 2.5.0.0 we get an aggregate exception here 
                // with an inner exception of {"Could not find index named: dynamic/TestDataObjects"}
                // Since Stream() doesn't really support choosing and building a dynamic index, 
                // running queries that don't specify a valid index is considered bad.
                var exception = Assert.Throws<AggregateException>(() =>
                {
                    while (streamEnumerator.MoveNext())
                    {
                        break; // Don't care about results - it would have thrown on MoveNext
                    }
                });

                // Aggregate exception is still a bit confusing here, but don't want to have a breaking change yet
                var interestingException = exception.InnerException;

                Assert.NotNull(interestingException);
                Assert.Equal(
                    "A streaming query needs an existing index to query. Please ensure you compose the underlying query with a name of an existing index to use. Check the inner exception for more details on the missing index the query attempted to use.",
                    interestingException.Message);
                Assert.IsType<IndexDoesNotExistsException>(interestingException.InnerException);
            }
        }

        
        [Fact]
        public void Stream_Should_Not_Throw_Invalid_Index_If_Index_Specified_In_Query()
        {
            // Arrange
            var testObj = new TestDataObject() { Name = "Test1" };
            var testObj1 = new TestDataObject() { Name = "Test2" };

            using (var docStore = NewDocumentStore())
            using (var session = docStore.OpenSession())
            {
                InsertTestData(session, testObj, testObj1);

                // Act
                var query = session.Query<TestDataObject>(new RavenDocumentsByEntityName().IndexName);

                // Assert: (1) Underlying query doesn't throw and (2) Stream doesn't throw
                Assert.DoesNotThrow(() =>
                {
                    var list = query.ToList();
                });

                var streamEnumerator = session.Advanced.Stream(query);

                // Because we've forced an index name in the query, the stream should use that and not throw
                Assert.DoesNotThrow(() =>
                {
                    while (streamEnumerator.MoveNext())
                    {
                        break; // Don't care about results - it would have thrown on MoveNext
                    }
                });

                // If we get here with no exception, we're good.
            }

        }


        #region test helpers

        private void InsertTestData(IDocumentSession session, params object[] testObjects)
        {
            foreach (var obj in testObjects)
            {
                session.Store(obj);
            }
            session.SaveChanges();
        }

        /// <summary>
        /// Dummy type for illustrating this test
        /// </summary>
        private class TestDataObject
        {
            public string Id { get; set; }
            public string Name { get; set; }
        }

        #endregion

    }
}