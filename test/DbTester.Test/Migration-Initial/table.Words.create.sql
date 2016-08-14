:setvar DatabaseName "DbTester_Test_ProcessHelperTest"
USE $(DatabaseName)

PRINT 'Creating Table...'

IF OBJECT_ID('dbo.Words') IS NOT NULL
BEGIN
	DROP TABLE dbo.Words
END

CREATE TABLE dbo.Words(
	Word nvarchar(256)
)