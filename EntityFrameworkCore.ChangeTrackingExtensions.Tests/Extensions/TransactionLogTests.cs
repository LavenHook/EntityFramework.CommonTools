#if EF_CORE
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace EntityFrameworkCore.ChangeTrackingExtensions.Tests
#else
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace EntityFramework.ChangeTrackingExtensions.Tests
#endif
{
    [TestClass]
    public class TransactionLogTests : TestInitializer
    {
        [TestMethod]
        public void TestTransactionLogInsert()
        {
            using (var context = CreateTestDbContext())
            {
                var post = new Post { Title = "test", Content = "test test test" };

                post.Tags.Add("first");
                post.Tags.Add("second");

                context.Posts.Add(post);

                var user = new User { Login = "admin" };

                post.Author = user;

                context.Users.Add(user);
                
                context.SaveChanges();
            }

            using (var context = CreateTestDbContext())
            {
                var logs = context.TransactionLogs.ToArray();

                Assert.IsNotNull(logs);
                Assert.AreEqual(2, logs.Length);
                Assert.AreEqual(logs[0].TransactionId, logs[1].TransactionId);
                Assert.AreEqual(logs[0].CreatedUtc, logs[1].CreatedUtc);

                Assert.AreEqual("Posts", logs[0].TableName);
                Assert.AreEqual(TransactionLog.INSERT, logs[0].Operation);
                Assert.IsInstanceOfType(logs[0].Entity, typeof(Post));
                Assert.AreEqual(1, logs[0].GetEntity<Entity>().Id);
                Assert.IsTrue(logs[0].GetEntity<Post>().Tags.SequenceEqual(new[] { "first", "second" }));

                Assert.AreEqual("Users", logs[1].TableName);
                Assert.AreEqual(TransactionLog.INSERT, logs[1].Operation);
                Assert.IsInstanceOfType(logs[1].Entity, typeof(User));
                Assert.AreEqual(1, logs[1].GetEntity<Entity>().Id);
                Assert.AreEqual("admin", logs[1].GetEntity<User>().Login);
            }
        }

        [TestMethod]
        public void TestTransactionLogUpdate()
        {
            using (var context = CreateTestDbContext())
            {
                var user = new User { Login = "admin" };
                var post = new Post { Title = "test", Content = "test test test", Author = user };
                
                context.Users.Add(user);
                context.Posts.Add(post);
                
                context.OriginalSaveChanges();
            }

            using (var context = CreateTestDbContext())
            {
                var user = context.Users.Single();
                var post = context.Posts.Single();

                post.Author.Login = "modified";
                post.Tags.Add("modified");

                context.SaveChanges();
            }

            using (var context = CreateTestDbContext())
            {
                var logs = context.TransactionLogs.ToArray();

                Assert.IsNotNull(logs);
                Assert.AreEqual(2, logs.Length);
                Assert.AreEqual(logs[0].TransactionId, logs[1].TransactionId);
                Assert.AreEqual(logs[0].CreatedUtc, logs[1].CreatedUtc);

                Assert.AreEqual("Users", logs[0].TableName);
                Assert.AreEqual(TransactionLog.UPDATE, logs[0].Operation);
                Assert.IsInstanceOfType(logs[0].Entity, typeof(User));
                Assert.AreEqual(1, logs[0].GetEntity<Entity>().Id);
                Assert.AreEqual("modified", logs[0].GetEntity<User>().Login);

                Assert.AreEqual("Posts", logs[1].TableName);
                Assert.AreEqual(TransactionLog.UPDATE, logs[1].Operation);
                Assert.IsInstanceOfType(logs[1].Entity, typeof(Post));
                Assert.AreEqual(1, logs[1].GetEntity<Entity>().Id);
                Assert.IsTrue(logs[1].GetEntity<Post>().Tags.SequenceEqual(new[] { "modified" }));
            }
        }

        [TestMethod]
        public void TestTransactionLogDelete()
        {
            using (var context = CreateTestDbContext())
            {
                var role = new Role { Name = "test" };

                context.Roles.Add(role);

                context.OriginalSaveChanges();
            }

            using (var context = CreateTestDbContext())
            {
                var role = context.Roles.Single();

                context.Roles.Remove(role);

                context.SaveChanges();
            }

            using (var context = CreateTestDbContext())
            {
                var logs = context.TransactionLogs.ToArray();

                Assert.IsNotNull(logs);
                Assert.AreEqual(1, logs.Length);

                Assert.AreEqual("Roles", logs[0].TableName);
                Assert.AreEqual(TransactionLog.DELETE, logs[0].Operation);
                Assert.IsInstanceOfType(logs[0].Entity, typeof(Role));
                Assert.AreEqual(1, logs[0].GetEntity<Entity>().Id);
            }
        }

        [TestMethod]
        public void TestTransactionLogSoftDelete()
        {
            using (var context = CreateTestDbContext())
            {
                var user = new User { Login = "admin" };
                var post = new Post { Title = "test", Content = "test test test", Author = user };

                context.Users.Add(user);
                context.Posts.Add(post);

                context.OriginalSaveChanges();
            }

            using (var context = CreateTestDbContext())
            {
                var user = context.Users.Single();
                var post = context.Posts.Single();

                context.Posts.Remove(post);
                context.Users.Remove(user);

                context.SaveChanges();
            }

            using (var context = CreateTestDbContext())
            {
                var logs = context.TransactionLogs.ToArray();

                Assert.IsNotNull(logs);
                Assert.AreEqual(2, logs.Length);
                Assert.AreEqual(logs[0].TransactionId, logs[1].TransactionId);
                Assert.AreEqual(logs[0].CreatedUtc, logs[1].CreatedUtc);

                Assert.AreEqual("Posts", logs[0].TableName);
                Assert.AreEqual(TransactionLog.UPDATE, logs[0].Operation);
                Assert.IsInstanceOfType(logs[0].Entity, typeof(Post));
                Assert.AreEqual(1, logs[0].GetEntity<Entity>().Id);
                Assert.IsTrue(logs[0].GetEntity<Post>().IsDeleted);

                Assert.AreEqual("Users", logs[1].TableName);
                Assert.AreEqual(TransactionLog.UPDATE, logs[1].Operation);
                Assert.IsInstanceOfType(logs[1].Entity, typeof(User));
                Assert.AreEqual(1, logs[1].GetEntity<Entity>().Id);
                Assert.IsTrue(logs[1].GetEntity<User>().IsDeleted);
            }
        }

        [TestMethod]
        public async Task TestTransactionLogCombined()
        {
            using (var context = CreateSqliteDbContext())
            {
                var user = new User { Login = "admin" };
                context.Users.Add(user);
                context.Roles.Add(new Role { Name = "test" });
                context.Posts.Add(new Post { Title = "first", Author = user });
                context.Posts.Add(new Post { Title = "first", Author = user });

                context.OriginalSaveChanges();
            }

            using (var context = CreateSqliteDbContext())
            {
                var user = context.Users.Single();
                var role = context.Roles.Single();
                var posts = context.Posts.ToList();

                // insert third post
                context.Posts.Add(new Post { Title = "third", Author = user });

                // update user and first post
                user.Login = "modified";
                posts[0].Tags.Add("modified");

                // delete second post and role
                context.Posts.Remove(posts[1]);
                context.Roles.Remove(role);

                await context.SaveChangesAsync();
            }

            using (var context = CreateSqliteDbContext())
            {
                var logs = context.TransactionLogs.ToArray();

                Assert.IsNotNull(logs);
                Assert.AreEqual(5, logs.Length);
                Assert.IsTrue(logs.All(l => l.TransactionId == logs[0].TransactionId));
                Assert.IsTrue(logs.All(l => l.CreatedUtc == logs[0].CreatedUtc));

                Assert.AreEqual("dbo", logs[0].Schema);
                Assert.AreEqual("Posts", logs[0].TableName);
                Assert.AreEqual(TransactionLog.INSERT, logs[0].Operation);
                Assert.IsInstanceOfType(logs[0].Entity, typeof(Post));
                Assert.AreEqual(3, logs[0].GetEntity<Post>().Id);

                Assert.AreEqual("dbo", logs[1].Schema);
                Assert.AreEqual("Users", logs[1].TableName);
                Assert.AreEqual(TransactionLog.UPDATE, logs[1].Operation);
                Assert.IsInstanceOfType(logs[1].Entity, typeof(User));
                Assert.AreEqual(1, logs[1].GetEntity<User>().Id);

                Assert.AreEqual("dbo", logs[2].Schema);
                Assert.AreEqual("Posts", logs[2].TableName);
                Assert.AreEqual(TransactionLog.UPDATE, logs[2].Operation);
                Assert.IsInstanceOfType(logs[2].Entity, typeof(Post));
                Assert.AreEqual(1, logs[2].GetEntity<Post>().Id);

                Assert.AreEqual("dbo", logs[3].Schema);
                Assert.AreEqual("Posts", logs[3].TableName);
                Assert.AreEqual(TransactionLog.UPDATE, logs[3].Operation);
                Assert.IsInstanceOfType(logs[3].Entity, typeof(Post));
                Assert.AreEqual(2, logs[3].GetEntity<Post>().Id);
                Assert.IsTrue(logs[3].GetEntity<Post>().IsDeleted);

                Assert.AreEqual("dbo", logs[4].Schema);
                Assert.AreEqual("Roles", logs[4].TableName);
                Assert.AreEqual(TransactionLog.DELETE, logs[4].Operation);
                Assert.IsInstanceOfType(logs[4].Entity, typeof(Role));
                Assert.AreEqual(1, logs[4].GetEntity<Role>().Id);
            }
        }
    }
}