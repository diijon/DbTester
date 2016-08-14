namespace DbTester.Contracts
{
    public interface ISettings
    {
        string Task { get; set; }
        string TargetConnectionStringTemplate { get; set; }
        string MigrationPath { get; set; }
        IDbCmdWaitTime DbCmdWaitTime { get; set; }
    }
    public interface IDbCmdWaitTime
    {
        int DeleteDatabase { get; set; }
        int CreateDatabase { get; set; }
    }
}