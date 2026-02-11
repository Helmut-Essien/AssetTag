namespace Shared.Constants
{
    /// <summary>
    /// Constants for asset status, condition, and other standardized values
    /// </summary>
    public static class AssetConstants
    {
        /// <summary>
        /// Standard asset status values
        /// </summary>
        public static class Status
        {
            public const string Available = "Available";
            public const string InUse = "In Use";
            public const string UnderMaintenance = "Under Maintenance";
            public const string Disposed = "Disposed";
            public const string Retired = "Retired";
            public const string Lost = "Lost";
            public const string Stolen = "Stolen";
            
            public static readonly string[] All = 
            {
                Available,
                InUse,
                UnderMaintenance,
                Disposed,
                Retired,
                Lost,
                Stolen
            };
        }

        /// <summary>
        /// Standard asset condition values
        /// </summary>
        public static class Condition
        {
            public const string Excellent = "Excellent";
            public const string Good = "Good";
            public const string Fair = "Fair";
            public const string Poor = "Poor";
            public const string Broken = "Broken";
            
            public static readonly string[] All = 
            {
                Excellent,
                Good,
                Fair,
                Poor,
                Broken
            };
            
            public static readonly string[] RequiresMaintenance = 
            {
                Fair,
                Poor,
                Broken
            };
        }

        /// <summary>
        /// Standard asset history actions
        /// </summary>
        public static class HistoryAction
        {
            public const string Added = "Added";
            public const string Purchased = "Purchased";
            public const string Maintenance = "Maintenance";
            public const string Transferred = "Transferred";
            public const string Assigned = "Assigned";
            public const string Unassigned = "Unassigned";
            public const string Disposed = "Disposed";
            public const string Updated = "Updated";
            public const string StatusChanged = "Status Changed";
        }

        /// <summary>
        /// Report configuration constants
        /// </summary>
        public static class Reports
        {
            public const int DefaultPageSize = 50;
            public const int MaxPageSize = 500;
            public const int WarrantyExpiryWarningDays = 90;
            public const int WarrantyExpiryCriticalDays = 30;
            public const int WarrantyExpiryHighDays = 60;
            public const int DefaultMaintenanceIntervalMonths = 6;
            public const int ChatHistoryMaxMessages = 20;
        }
    }
}