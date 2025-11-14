using System;
using System.Net;
using System.Text.Json;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Azure.Data.Tables;
using Azure;
using Azure.Storage.Queues;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ABCRetailByRH.Functions
{
    public class OrderFunctions
    {
        private readonly QueueClient _queue;
        private readonly QueueClient _finalizeQueue;
        private readonly TableClient _ordersTable;
        private readonly TableClient _productsTable;
        private readonly ILogger _log;

        private const string OrdersPartition = "ORDERS";
        private const string StorageConnectionName = "AzureWebJobsStorage";

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            PropertyNameCaseInsensitive = true
        };

        // DTOs ----------------------------------------------------------
        public record OrderItemDto(
            string ProductId,
            string Name,
            double UnitPrice,
            int Quantity
        );

        public record OrderCheckoutDto(
            string? OrderId,
            string Customer,
            double Total,
            List<OrderItemDto> Items,
            DateTime? TimestampUtc = null
        );

        // Constructor ---------------------------------------------------
        public OrderFunctions(IConfiguration cfg, ILoggerFactory lf)
        {
            var cs = cfg[StorageConnectionName]
                     ?? throw new InvalidOperationException($"Missing {StorageConnectionName}");

            _queue = new QueueClient(cs, cfg["OrdersQueueName"] ?? "orders",
                new QueueClientOptions { MessageEncoding = QueueMessageEncoding.Base64 });

            _finalizeQueue = new QueueClient(cs, cfg["OrdersFinalizeQueueName"] ?? "orders-finalize",
                new QueueClientOptions { MessageEncoding = QueueMessageEncoding.Base64 });

            _queue.CreateIfNotExists();
            _finalizeQueue.CreateIfNotExists();

            _ordersTable = new TableClient(cs, cfg["OrdersTableName"] ?? "Orders");
            _ordersTable.CreateIfNotExists();

            _productsTable = new TableClient(cs, "Products");
            _productsTable.CreateIfNotExists();

            _log = lf.CreateLogger<OrderFunctions>();
        }

        // =============================================================
        // 1) ENQUEUE ORDER
        // =============================================================
        [Function("Orders_Enqueue")]
        public async Task<HttpResponseData> Enqueue(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "orders/enqueue")] HttpRequestData req)
        {
            var dto = await JsonSerializer.DeserializeAsync<OrderCheckoutDto>(req.Body, JsonOpts)
                      ?? new OrderCheckoutDto(null, "Unknown", 0, new List<OrderItemDto>());

            var checkoutId = dto.OrderId ?? Guid.NewGuid().ToString("N");

            var outgoing = dto with
            {
                OrderId = checkoutId,
                TimestampUtc = DateTime.UtcNow
            };

            await _queue.SendMessageAsync(JsonSerializer.Serialize(outgoing, JsonOpts));

            var res = req.CreateResponse(HttpStatusCode.Accepted);
            await res.WriteAsJsonAsync(new { queued = true, OrderId = checkoutId });
            return res;
        }

        // =============================================================
        // 2) PROCESS ORDER (WRITE LINES)
        // =============================================================
        [Function("Orders_Process")]
        public async Task ProcessAsync(
            [QueueTrigger("%OrdersQueueName%", Connection = StorageConnectionName)] string message)
        {
            var dto = JsonSerializer.Deserialize<OrderCheckoutDto>(message, JsonOpts)
                      ?? throw new Exception("Invalid order payload.");

            if (dto.Items == null || dto.Items.Count == 0)
            {
                _log.LogWarning("Orders_Process received a checkout with ZERO items.");
                return;
            }

            var orderId = dto.OrderId ?? Guid.NewGuid().ToString("N");
            var timestamp = dto.TimestampUtc ?? DateTime.UtcNow;

            foreach (var item in dto.Items)
            {
                var lineId = Guid.NewGuid().ToString("N");

                var entity = new TableEntity(OrdersPartition, lineId)
                {
                    ["OrderId"] = orderId,
                    ["Username"] = dto.Customer,
                    ["ProductId"] = item.ProductId,
                    ["ProductName"] = item.Name,
                    ["Quantity"] = item.Quantity,
                    ["UnitPrice"] = item.UnitPrice,

                    // IMPORTANT — required by your MVC app
                    ["TotalPrice"] = item.UnitPrice * item.Quantity,

                    ["CreatedAtUtc"] = timestamp,
                    ["Status"] = "Submitted"
                };

                await _ordersTable.UpsertEntityAsync(entity, TableUpdateMode.Replace);

                // Deduct stock safely
                try
                {
                    var prod = await _productsTable.GetEntityAsync<TableEntity>("PRODUCTS", item.ProductId);
                    int stock = Convert.ToInt32(prod.Value["Stock"]);
                    prod.Value["Stock"] = Math.Max(0, stock - item.Quantity);
                    await _productsTable.UpdateEntityAsync(prod.Value, prod.Value.ETag, TableUpdateMode.Merge);
                }
                catch (Exception ex)
                {
                    _log.LogError(ex, "Failed to deduct stock");
                }
            }

            // Queue finalize
            await _finalizeQueue.SendMessageAsync(JsonSerializer.Serialize(new { OrderId = orderId }, JsonOpts));
        }

        // =============================================================
        // 3) FINALIZE ORDER
        // =============================================================
        [Function("Orders_Finalize")]
        public async Task FinalizeAsync(
            [QueueTrigger("%OrdersFinalizeQueueName%", Connection = StorageConnectionName)] string message)
        {
            var data = JsonSerializer.Deserialize<Dictionary<string, string>>(message, JsonOpts);
            if (data == null || !data.TryGetValue("OrderId", out var orderId))
                return;

            var rows = _ordersTable.Query<TableEntity>($"PartitionKey eq '{OrdersPartition}' and OrderId eq '{orderId}'");

            foreach (var e in rows)
            {
                e["Status"] = "Processed";
                e["ProcessedUtc"] = DateTime.UtcNow;
                await _ordersTable.UpdateEntityAsync(e, e.ETag, TableUpdateMode.Merge);
            }
        }

        // =============================================================
        // 4) LIST ALL ORDER LINES
        // =============================================================
        [Function("Orders_List")]
        public async Task<HttpResponseData> ListAsync(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "orders")] HttpRequestData req)
        {
            var filter = $"PartitionKey eq '{OrdersPartition}'";
            var items = new List<object>();

            await foreach (var e in _ordersTable.QueryAsync<TableEntity>(filter))
            {
                items.Add(new
                {
                    OrderId = e.GetString("OrderId") ?? "",
                    Username = e.GetString("Username") ?? "",
                    ProductName = e.GetString("ProductName") ?? "",
                    Quantity = e.GetInt32("Quantity"),
                    UnitPrice = e.GetDouble("UnitPrice"),
                    TotalPrice = e.GetDouble("TotalPrice"),
                    Status = e.GetString("Status") ?? "",
                    CreatedAtUtc = e.GetDateTime("CreatedAtUtc")
                });
            }

            var res = req.CreateResponse(HttpStatusCode.OK);
            await res.WriteAsJsonAsync(items);
            return res;
        }
    }
}
