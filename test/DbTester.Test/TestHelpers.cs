using Serilog;
using System;
using System.Data.Common;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Reflection;

namespace DbTester.Test
{
    public static class TestHelpers
    {
        /*///Works
        public static DbConnection GetConnection(string connectionString)
        {
            DbConnection cnn = new SqlConnection(connectionString);

            // to get profiling times, we have to wrap whatever connection we're using in a ProfiledDbConnection
            // when MiniProfiler.Current is null, this connection will not record any database timings
            if (StackExchange.Profiling.MiniProfiler.Current != null)
            {
                cnn = new StackExchange.Profiling.Data.ProfiledDbConnection(cnn, StackExchange.Profiling.MiniProfiler.Current);
            }

            //cnn.Open();
            return cnn;
        }
        public static DbConnection GetConnection(string connectionString)
        {
            var cnn = new SqlConnection(connectionString);
            return new StackExchange.Profiling.Data.ProfiledDbConnection(cnn, StackExchange.Profiling.MiniProfiler.Current);
        }
        /**/

        public static Stream FindResourceStream(this Type type, string name)
        {
            return (from r in type.Assembly.GetManifestResourceNames()
                    where r.EndsWith("." + name)
                    select type.Assembly
                               .GetManifestResourceStream(r))
                   .FirstOrDefault();
        }

        public static T FindResource<T>(this Type type, string name, Func<Stream, T> transformer)
        {
            return transformer(FindResourceStream(type, name));
        }

        public static string FindResourceString(this Type type, string name)
        {
            return FindResource(type, name,
                s =>
                {
                    using (var reader = new StreamReader(s))
                        return reader.ReadToEnd();
                });
        }

        public static void TestInitialize(ref ILogger log, ref Contracts.ITester tester, Contracts.ISettings settings)
        {
            tester = new Tester(settings);
            log = new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .WriteTo.ColoredConsole()
                .CreateLogger();
        }

        public static string GetCurrentFolder()
        {
            return Path.GetDirectoryName(Assembly.GetExecutingAssembly().CodeBase).Replace(@"file:\", string.Empty);
        }
    }
}
