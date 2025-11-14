using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using ABCRetailByRH.Models;

namespace ABCRetailByRH.Services
{
    public interface IAzureStorageService
    {
        // Customers
        List<Customer> GetAllCustomers();
        Customer? GetCustomer(string partitionKey, string rowKey);
        void AddCustomer(Customer customer);
        void UpdateCustomer(Customer customer);
        void DeleteCustomer(string partitionKey, string rowKey);

        // Products
        List<Product> GetAllProducts();
        Product? GetProduct(string partitionKey, string rowKey);
        void AddProduct(Product product, IFormFile? imageFile);
        void UpdateProduct(Product product, IFormFile? newImageFile);
        void DeleteProduct(string partitionKey, string rowKey);

        // Orders (Table Storage)
        List<Order> ListOrders(string? status = null, int top = 200);
        List<Order> ListOrdersByCustomer(string customerUsername, string? status = null, int top = 200);
        Order? GetOrder(string rowKey, string partitionKey = "ORDERS");
        void UpdateOrderStatus(string rowKey, string newStatus, string partitionKey = "ORDERS");
        void AttachProofOfPayment(string rowKey, IFormFile file, string partitionKey = "ORDERS");

        // Queue (orders)
        void EnqueueMessage(string message);
        string? DequeueMessage();
        int GetQueueLength();

        // Files (generic)
        void UploadFile(IFormFile file);
        List<string> ListFiles();
        byte[] DownloadFile(string fileName, out string contentType);
        void DeleteFile(string fileName);
    }
}
