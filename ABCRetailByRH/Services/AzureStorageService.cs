using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ABCRetailByRH.Models;
using Azure;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Queues;
using Azure.Storage.Files.Shares;
using Azure.Storage.Files.Shares.Models;
using Microsoft.AspNetCore.Http;

namespace ABCRetailByRH.Services
{
    public class AzureStorageService : IAzureStorageService
    {
        private readonly TableServiceClient _tables;
        private readonly BlobServiceClient _blobs;
        private readonly QueueServiceClient _queues;
        private readonly ShareServiceClient _shares;

        private const string CustomersTable = "Customers";
        private const string ProductsTable = "Products";
        private const string OrdersTable = "Orders";
        private const string OrdersQueue = "orders";
        private const string ProductImages = "product-images";
        private const string ContractsShare = "contracts"; // reuse for PoP files

        public AzureStorageService(string storageConnectionString)
        {
            _tables = new TableServiceClient(storageConnectionString);
            _blobs = new BlobServiceClient(storageConnectionString);
            _queues = new QueueServiceClient(storageConnectionString);
            _shares = new ShareServiceClient(storageConnectionString);

            _tables.CreateTableIfNotExists(CustomersTable);
            _tables.CreateTableIfNotExists(ProductsTable);
            _tables.CreateTableIfNotExists(OrdersTable);

            var container = _blobs.GetBlobContainerClient(ProductImages);
            container.CreateIfNotExists(PublicAccessType.Blob);

            var queue = _queues.GetQueueClient(OrdersQueue);
            queue.CreateIfNotExists();

            var share = _shares.GetShareClient(ContractsShare);
            share.CreateIfNotExists();
        }

        // ---------- Customers ----------
        public List<Customer> GetAllCustomers()
            => _tables.GetTableClient(CustomersTable).Query<Customer>().ToList();

        public Customer? GetCustomer(string partitionKey, string rowKey)
        {
            var table = _tables.GetTableClient(CustomersTable);
            try { return table.GetEntity<Customer>(partitionKey, rowKey).Value; }
            catch (RequestFailedException ex) when (ex.Status == 404) { return null; }
        }

        public void AddCustomer(Customer customer)
        {
            if (string.IsNullOrWhiteSpace(customer.PartitionKey)) customer.PartitionKey = "CUSTOMERS";
            if (string.IsNullOrWhiteSpace(customer.RowKey)) customer.RowKey = Guid.NewGuid().ToString("N");
            _tables.GetTableClient(CustomersTable).AddEntity(customer);
        }

        public void UpdateCustomer(Customer customer)
            => _tables.GetTableClient(CustomersTable).UpsertEntity(customer, TableUpdateMode.Merge);

        public void DeleteCustomer(string partitionKey, string rowKey)
            => _tables.GetTableClient(CustomersTable).DeleteEntity(partitionKey, rowKey, ETag.All);

        // ---------- Products ----------
        public List<Product> GetAllProducts()
            => _tables.GetTableClient(ProductsTable).Query<Product>().ToList();

        public Product? GetProduct(string partitionKey, string rowKey)
        {
            var table = _tables.GetTableClient(ProductsTable);
            try { return table.GetEntity<Product>(partitionKey, rowKey).Value; }
            catch (RequestFailedException ex) when (ex.Status == 404) { return null; }
        }

        public void AddProduct(Product product, IFormFile? imageFile)
        {
            if (string.IsNullOrWhiteSpace(product.PartitionKey)) product.PartitionKey = "PRODUCTS";
            if (string.IsNullOrWhiteSpace(product.RowKey)) product.RowKey = Guid.NewGuid().ToString("N");
            if (imageFile != null && imageFile.Length > 0)
                product.ImageUrl = UploadProductImageInternal(imageFile);
            _tables.GetTableClient(ProductsTable).AddEntity(product);
        }

        public void UpdateProduct(Product product, IFormFile? newImageFile)
        {
            if (newImageFile != null && newImageFile.Length > 0)
                product.ImageUrl = UploadProductImageInternal(newImageFile);
            _tables.GetTableClient(ProductsTable).UpsertEntity(product, TableUpdateMode.Merge);
        }

        public void DeleteProduct(string partitionKey, string rowKey)
        {
            var p = GetProduct(partitionKey, rowKey);
            if (p != null && !string.IsNullOrWhiteSpace(p.ImageUrl))
            {
                try
                {
                    var uri = new Uri(p.ImageUrl);
                    var blobName = uri.Segments.Last();
                    _blobs.GetBlobContainerClient(ProductImages).DeleteBlobIfExists(blobName);
                }
                catch { }
            }
            _tables.GetTableClient(ProductsTable).DeleteEntity(partitionKey, rowKey, ETag.All);
        }

        private string UploadProductImageInternal(IFormFile file)
        {
            var container = _blobs.GetBlobContainerClient(ProductImages);
            var blobName = $"{Guid.NewGuid():N}{Path.GetExtension(file.FileName)}";
            var blob = container.GetBlobClient(blobName);
            using var s = file.OpenReadStream();
            blob.Upload(s, new BlobHttpHeaders { ContentType = file.ContentType });
            return blob.Uri.ToString();
        }

        // ---------- Orders (Table Storage) ----------
        public List<Order> ListOrders(string? status = null, int top = 200)
        {
            var table = _tables.GetTableClient(OrdersTable);

            IEnumerable<Order> query = table.Query<Order>(o => o.PartitionKey == "ORDERS");

            if (!string.IsNullOrWhiteSpace(status))
                query = query.Where(o => o.Status == status);

            return query.Take(top).ToList();
        }

        public List<Order> ListOrdersByCustomer(string customerUsername, string? status = null, int top = 200)
        {
            var table = _tables.GetTableClient(OrdersTable);

            IEnumerable<Order> query = table.Query<Order>(o => o.PartitionKey == "ORDERS" && o.Username == customerUsername);

            if (!string.IsNullOrWhiteSpace(status))
                query = query.Where(o => o.Status == status);

            return query.Take(top).ToList();
        }

        public Order? GetOrder(string rowKey, string partitionKey = "ORDERS")
        {
            var table = _tables.GetTableClient(OrdersTable);
            try { return table.GetEntity<Order>(partitionKey, rowKey).Value; }
            catch (RequestFailedException ex) when (ex.Status == 404) { return null; }
        }

        public void UpdateOrderStatus(string rowKey, string newStatus, string partitionKey = "ORDERS")
        {
            var table = _tables.GetTableClient(OrdersTable);
            var ord = GetOrder(rowKey, partitionKey);
            if (ord == null) return;
            ord.Status = newStatus;
            table.UpsertEntity(ord, TableUpdateMode.Merge);
        }

        public void AttachProofOfPayment(string rowKey, IFormFile file, string partitionKey = "ORDERS")
        {
            if (file == null || file.Length == 0) return;

            var order = GetOrder(rowKey, partitionKey);
            if (order == null) return;

            var share = _shares.GetShareClient(ContractsShare);
            share.CreateIfNotExists();
            var root = share.GetRootDirectoryClient();

            var safeName = $"proof-{rowKey}-{DateTime.UtcNow:yyyyMMddHHmmss}{Path.GetExtension(file.FileName)}";
            var fc = root.GetFileClient(safeName);
            using var s = file.OpenReadStream();
            fc.Create(file.Length);
            fc.UploadRange(new HttpRange(0, file.Length), s);

            order.ContractFileName = safeName;
            order.ContractOriginalFileName = Path.GetFileName(file.FileName);
            order.ContractContentType = file.ContentType;

            _tables.GetTableClient(OrdersTable).UpsertEntity(order, TableUpdateMode.Merge);
        }

        // ---------- Queue ----------
        public void EnqueueMessage(string message)
            => _queues.GetQueueClient(OrdersQueue).SendMessage(message ?? string.Empty);

        public string? DequeueMessage()
        {
            var q = _queues.GetQueueClient(OrdersQueue);
            var msg = q.ReceiveMessage();
            if (msg.Value == null) return null;
            q.DeleteMessage(msg.Value.MessageId, msg.Value.PopReceipt);
            return msg.Value.MessageText;
        }

        public int GetQueueLength()
        {
            var props = _queues.GetQueueClient(OrdersQueue).GetProperties();
            return props?.Value?.ApproximateMessagesCount ?? 0;
        }

        // ---------- Generic files ----------
        public void UploadFile(IFormFile file)
        {
            if (file == null || file.Length == 0) return;
            var share = _shares.GetShareClient(ContractsShare);
            share.CreateIfNotExists();
            var root = share.GetRootDirectoryClient();
            var safe = $"{DateTime.UtcNow:yyyyMMddHHmmss}-{Path.GetFileName(file.FileName)}";
            using var s = file.OpenReadStream();
            var f = root.GetFileClient(safe);
            f.Create(file.Length);
            f.UploadRange(new HttpRange(0, file.Length), s);
        }

        public List<string> ListFiles()
        {
            var share = _shares.GetShareClient(ContractsShare);
            share.CreateIfNotExists();
            var root = share.GetRootDirectoryClient();
            return root.GetFilesAndDirectories()
                       .Where(i => !i.IsDirectory)
                       .Select(i => i.Name)
                       .ToList();
        }

        public byte[] DownloadFile(string fileName, out string contentType)
        {
            contentType = "application/octet-stream";
            var share = _shares.GetShareClient(ContractsShare);
            var root = share.GetRootDirectoryClient();
            var file = root.GetFileClient(fileName);
            if (!file.Exists()) return Array.Empty<byte>();
            var props = file.GetProperties();
            contentType = props.Value.ContentType ?? contentType;
            var dl = file.Download();
            using var ms = new MemoryStream();
            dl.Value.Content.CopyTo(ms);
            return ms.ToArray();
        }

        public void DeleteFile(string fileName)
        {
            var share = _shares.GetShareClient(ContractsShare);
            var root = share.GetRootDirectoryClient();
            var file = root.GetFileClient(fileName);
            file.DeleteIfExists();
        }
    }
}
