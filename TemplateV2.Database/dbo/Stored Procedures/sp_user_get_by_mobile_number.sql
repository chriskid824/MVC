﻿CREATE PROCEDURE [dbo].[sp_user_get_by_mobile_number]
	@Mobile_Number VARCHAR(256)
AS
BEGIN
   SELECT
		[U].[Id],
		[U].[Username],
		[U].[Email_Address],
		[U].[Mobile_Number],
		[U].[First_Name],
		[U].[Last_Name],
		[U].[Registration_Confirmed],
		[U].[Password_Hash],
		[U].[Is_Locked_Out],
		[U].[Lockout_End],
		[U].[Invalid_Login_Attempts],
		[U].[Created_By],
		[U].[Created_Date],
		[U].[Updated_By],
		[U].[Updated_Date],
		[U].Is_Deleted,
		[U].Is_Enabled
   FROM   [User] [U](NOLOCK)
   WHERE  [U].[Mobile_Number] = @Mobile_Number
   AND [U].Is_Deleted = 0;
END