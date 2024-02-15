using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using CsvHelper;

namespace OaiFuncCall.Models;

public class Product{
  public int ProductId { get; set; }
  public string? ProductName { get; set; }
  public int UnitsInStock { get; set; }
  public float UnitPrice { get; set; }

  // Load products from a csv file named products.csv in the wwwroot folder
  public static List<Product> LoadProducts() {
    var products = new List<Product>();
    using (var reader = new StreamReader(Path.Combine("wwwroot", "products.csv"))) {
      using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture)) {
          products = csv.GetRecords<Product>().ToList();
      }
  }
    return products;
  }

  public override string ToString() {
    return $"Product ID: {ProductId}, Product Name: {ProductName}, Units In Stock: {UnitsInStock}, Unit Price: {UnitPrice}";
  }
}
