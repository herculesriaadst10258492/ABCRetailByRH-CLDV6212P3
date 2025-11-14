using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Azure;
using Azure.Data.Tables;
using Microsoft.AspNetCore.Http;

namespace ABCRetailByRH.Models
{
    public class Order : ITableEntity
    {
        public string PartitionKey { get; set; } = "ORDERS";
        public string RowKey { get; set; } = Guid.NewGuid().ToString("N");
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }

        // Keep Id mapped to RowKey for views
        [NotMapped]
        public string Id
        {
            get => RowKey;
            set => RowKey = string.IsNullOrWhiteSpace(value) ? RowKey : value;
        }

        // IMPORTANT: OrderId must be persisted
        public string OrderId { get; set; } = string.Empty;

        public string CustomerId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;

        public string ProductId { get; set; } = string.Empty;
        public string? ProductName { get; set; }

        public int Quantity { get; set; } = 1;
        public double UnitPrice { get; set; }
        public double TotalPrice { get; set; }

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        public string Status { get; set; } = "Submitted";

        public string? ContractFileName { get; set; }
        public string? ContractOriginalFileName { get; set; }
        public string? ContractContentType { get; set; }

        [NotMapped]
        public IFormFile? ContractFile { get; set; }
    }
}
