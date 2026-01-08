using IBAPI.ExecuteMilestone.Model;
using IBAPI.MetadataMilestone.DbContext;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using IBAPI.MetadataMilestone.Model;

public static class ServiceStart
{
    public static void RegisterReceiveMetadataStart()
    {
        var rs = new ResponseModel { Status = false, Message = "Fail" };

        var dt = DbContextConnection.ExecuteQuery(
        "SELECT * FROM Users WHERE Age > @Age",
        new SqlParameter("@Age", 18)
        );

        List<Guid> metadataIds = new List<Guid>();

        foreach (DataRow row in dt.Rows)
        {
            if (row["MetadataId"] == DBNull.Value)
                continue;

            var value = row["MetadataId"]?.ToString();

            if (!string.IsNullOrWhiteSpace(value))
                metadataIds.Add(Guid.Parse(value));
        }

        foreach (var metadata in metadataIds)
        {
            var param = new MetadataInput
            {
                MetadataId = metadata
            };

            rs = MilestoneServices.GetMetadataLiveViewer(param);
        }
    }
}
