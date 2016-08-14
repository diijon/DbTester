:ON Error EXIT

:setvar DatabaseName "DbTester_Test_ProcessHelperTest"

IF EXISTS(
	SELECT [name] FROM sys.databases
	WHERE [name] NOT IN('master','tempdb','model','msdb')
	AND [name] = '$(DatabaseName)'
)
BEGIN
	DROP DATABASE $(DatabaseName)
END

CREATE DATABASE $(DatabaseName)

:r table.Words.create.sql
GO