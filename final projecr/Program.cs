using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AlmoniaBakery
{
    // ----------classes ----------
    public class Item
    {
        public int ItemID { get; set; }
        public string Name { get; set; }
        public double Price { get; set; }

        public Item(int id, string name, double price)
        {
            ItemID = id;
            Name = name;
            Price = price;
        }
    }

    public class BakeryItem : Item
    {
        public int Quantity { get; set; }

        public BakeryItem(int id, string name, double price, int qty) : base(id, name, price)
        {
            Quantity = qty;
        }
    }

    public class Order
    {
        public int OrderID { get; set; }
        public string CustomerName { get; set; }
        public List<BakeryItem> Items { get; set; } = new List<BakeryItem>();
        public double DiscountRate { get; set; } 
        public DateTime Date { get; set; }

        public Order(int id, string customer, double discount)
        {
            OrderID = id;
            CustomerName = customer;
            DiscountRate = discount;
            Date = DateTime.Now;
        }

        public double Subtotal() => Items.Sum(i => i.Price * i.Quantity);
        public double Discount() => Subtotal() * DiscountRate;
        public double Tax() => (Subtotal() - Discount()) * 0.03;
        public double Total() => Subtotal() - Discount() + Tax();
    }

    // ---------- CSV thigns ----------
    public static class FileManager
    {
        public static void BackupIfExists(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    string bak = path + ".bak";
                    File.Copy(path, bak, true);
                }
            }
            catch { }
        }

        public static string Escape(string s)
        {
            if (s == null) return "";
            if (s.Contains(",") || s.Contains("\"") || s.Contains("\n"))
                return "\"" + s.Replace("\"", "\"\"") + "\"";
            return s;
        }

        public static string[] SplitCsvLine(string line)
        {
            var parts = new List<string>();
            bool inQuotes = false;
            string cur = "";
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        cur += '"';
                        i++;
                    }
                    else inQuotes = !inQuotes;
                    continue;
                }
                if (c == ',' && !inQuotes)
                {
                    parts.Add(cur);
                    cur = "";
                }
                else cur += c;
            }
            parts.Add(cur);
            return parts.ToArray();
        }

        public static void WriteAllLinesSafe(string path, IEnumerable<string> lines)
        {
            try
            {
                BackupIfExists(path);
                File.WriteAllLines(path, lines);
            }
            catch (Exception)
            {
          
                try
                {
                    using (var sw = new StreamWriter(path, false))
                    {
                        foreach (var l in lines) sw.WriteLine(l);
                    }
                }
                catch { }
            }
        }

        public static string[] ReadAllLinesSafe(string path)
        {
            try
            {
                if (!File.Exists(path)) return new string[0];
                return File.ReadAllLines(path);
            }
            catch
            {
                return new string[0];
            }
        }
    }

    // ---------- Bakery System ---------
    public class BakerySystem
    {
        private List<BakeryItem> items = new List<BakeryItem>();
        private List<Order> orders = new List<Order>();
        private int nextItemID = 1;
        private int nextOrderID = 1;

        private const string ITEMS_FILE = "items.csv";
        private const string ORDERS_FILE = "orders.csv";

        public BakerySystem()
        {
            LoadItems();
            LoadOrders();
        }

        // ----------------- Input helpers -----------------
        private static string ReadNonEmpty(string prompt)
        {
            while (true)
            {
                Console.Write(prompt);
                string s = Console.ReadLine();
                if (!string.IsNullOrWhiteSpace(s) && s.Any(char.IsLetterOrDigit))
                    return s.Trim();
                Console.WriteLine("Input cannot be empty and must contain at least one letter/number. Try again.");
            }
        }

        private static int ReadIntInRange(string prompt, int min, int max)
        {
            while (true)
            {
                Console.Write(prompt);
                if (int.TryParse(Console.ReadLine(), out int v) && v >= min && v <= max)
                    return v;
                Console.WriteLine($"Enter integer between {min} and {max}.");
            }
        }

        private static double ReadDoubleInRange(string prompt, double min, double max)
        {
            while (true)
            {
                Console.Write(prompt);
                if (double.TryParse(Console.ReadLine(), out double v) && v >= min && v <= max)
                    return v;
                Console.WriteLine($"Enter number between {min} and {max}.");
            }
        }

        private static void Pause()
        {
            Console.WriteLine("\nPress ENTER to continue...");
            Console.ReadLine();
        }

        // ----------------- Item management -----------------
        public void AddItem()
        {
            Console.Clear();
            Console.WriteLine("=== Add New Item ===");
            Console.WriteLine("(Type CANCEL at any time to abort)\n");

            string name = ReadStringWithCancel("Name: ");
            if (name == null) { Console.WriteLine("Cancelled."); Pause(); return; }

            double? price = ReadDoubleWithCancel("Price (P): ");
            if (price == null) { Console.WriteLine("Cancelled."); Pause(); return; }

            int? qty = ReadIntWithCancel("Initial stock quantity: ");
            if (qty == null) { Console.WriteLine("Cancelled."); Pause(); return; }

            var item = new BakeryItem(nextItemID++, name, price.Value, qty.Value);
            items.Add(item);
            SaveItems();
            Console.WriteLine("Item added.");
            Pause();
        }

        public void EditItem()
        {
            Console.Clear();
            if (!items.Any()) { Console.WriteLine("No items to edit."); Pause(); return; }
            ViewItemsTable();
            int id = ReadIntInRange("Enter Item ID to edit (0 cancel): ", 0, int.MaxValue);
            if (id == 0) return;
            var it = items.FirstOrDefault(i => i.ItemID == id);
            if (it == null) { Console.WriteLine("Not found."); Pause(); return; }

            Console.WriteLine($"Editing item #{it.ItemID} - {it.Name}");
            Console.Write("New name (leave blank to keep): ");
            string newName = Console.ReadLine();
            double newPrice = ReadDoubleInRange($"New price (current {it.Price:F2}): ", 0.01, 10000);

            if (!string.IsNullOrWhiteSpace(newName)) it.Name = newName.Trim();
            it.Price = newPrice;
            SaveItems();
            Console.WriteLine("Item updated.");
            Pause();
        }

        public void UpdateStock()
        {
            Console.Clear();
            if (!items.Any()) { Console.WriteLine("No items."); Pause(); return; }
            ViewItemsTable();
            int id = ReadIntInRange("Enter Item ID to update stock (0 cancel): ", 0, int.MaxValue);
            if (id == 0) return;
            var it = items.FirstOrDefault(i => i.ItemID == id);
            if (it == null) { Console.WriteLine("Not found."); Pause(); return; }

            int change = ReadIntInRange("Enter stock change (positive to add, negative to remove): ", -10000, 10000);
            int newQty = it.Quantity + change;
            if (newQty < 0) { Console.WriteLine("Resulting stock cannot be negative."); Pause(); return; }
            it.Quantity = newQty;
            SaveItems();
            Console.WriteLine($"Stock updated. New quantity: {it.Quantity}");
            Pause();
        }

        public void RemoveItem()
        {
            Console.Clear();
            if (!items.Any()) { Console.WriteLine("No items to remove."); Pause(); return; }
            ViewItemsTable();
            int id = ReadIntInRange("Enter Item ID to remove (0 cancel): ", 0, int.MaxValue);
            if (id == 0) return;
            var it = items.FirstOrDefault(i => i.ItemID == id);
            if (it == null) { Console.WriteLine("Not found."); Pause(); return; }

            Console.Write($"Confirm remove '{it.Name}'? (Y/N): ");
            string ans = Console.ReadLine().Trim().ToUpper();
            if (ans == "Y")
            {
                items.Remove(it);
                SaveItems();
                Console.WriteLine("Item removed.");
            }
            else Console.WriteLine("Cancelled.");
            Pause();
        }

        public void ViewItemsTable()
        {
            Console.Clear();
            Console.WriteLine("=== Items ===");
            if (!items.Any()) { Console.WriteLine("No items."); return; }

            Console.WriteLine("{0,-6}{1,-28}{2,10}{3,12}", "ID", "Name", "Price", "Stock");
            Console.WriteLine(new string('-', 60));
            foreach (var it in items.OrderBy(i => i.ItemID))
                Console.WriteLine("{0,-6}{1,-28}{2,10:F2}{3,12}", it.ItemID, it.Name, it.Price, it.Quantity);
        }

        public void SearchItems()
        {
            Console.Clear();
            Console.WriteLine("=== Search Items ===");
            string q = ReadNonEmpty("Enter search keyword: ").ToLower();
            var results = items.Where(i => i.Name.ToLower().Contains(q)).ToList();
            if (!results.Any()) { Console.WriteLine("No results."); Pause(); return; }

            Console.WriteLine("{0,-6}{1,-28}{2,10}{3,12}", "ID", "Name", "Price", "Stock");
            Console.WriteLine(new string('-', 60));
            foreach (var it in results) Console.WriteLine("{0,-6}{1,-28}{2,10:F2}{3,12}", it.ItemID, it.Name, it.Price, it.Quantity);
            Pause();
        }

        // ----------------- Orders -----------------
        public void CreateOrder()
        {
            Console.Clear();
            if (!items.Any()) { Console.WriteLine("No items available."); Pause(); return; }

            Console.WriteLine("=== Create Order ===");
            string customer = ReadNonEmpty("Customer name: ");
            double discountPercent = ReadDoubleInRange("Discount percent (0-100): ", 0, 100);
            double discount = discountPercent / 100.0;
            var order = new Order(nextOrderID++, customer, discount);

            while (true)
            {
                Console.Clear();
                Console.WriteLine($"Building order for {order.CustomerName}\n");
                ViewItemsTable();
                Console.WriteLine("\nCurrent order items:");
                if (!order.Items.Any()) Console.WriteLine(" (none)");
                else
                {
                    foreach (var oi in order.Items)
                        Console.WriteLine($" - {oi.Name} x{oi.Quantity} @ {oi.Price:F2}");
                }

                Console.WriteLine("\nOptions:");
                Console.WriteLine("1) Add item ");
                Console.WriteLine("2) Remove item from order");
                Console.WriteLine("3) Finish  ");
                Console.WriteLine("0) Cancel order");
                int opt = ReadIntInRange("> ", 0, 3);

                if (opt == 0)
                {
                    foreach (var oi in order.Items)
                    {
                        var orig = items.FirstOrDefault(x => x.ItemID == oi.ItemID);
                        if (orig != null) orig.Quantity += oi.Quantity;
                    }
                    Console.WriteLine("Order cancelled.");
                    Pause();
                    return;
                }
                else if (opt == 1)
                {
                    int id = ReadIntInRange("Enter Item ID to add: ", 1, int.MaxValue);
                    var it = items.FirstOrDefault(x => x.ItemID == id);
                    if (it == null) { Console.WriteLine("Invalid ID."); Pause(); continue; }
                    if (it.Quantity <= 0) { Console.WriteLine("Out of stock."); Pause(); continue; }

                    int max = it.Quantity;
                    int qty = ReadIntInRange($"Quantity (1 - {max}): ", 1, max);

                    order.Items.Add(new BakeryItem(it.ItemID, it.Name, it.Price, qty));
                    it.Quantity -= qty;
                    Console.WriteLine("Added.");
                    Pause();
                }
                else if (opt == 2)
                {
                    if (!order.Items.Any()) { Console.WriteLine("No items to remove."); Pause(); continue; }

                    Console.WriteLine("Order items:");
                    for (int i = 0; i < order.Items.Count; i++)
                        Console.WriteLine($"{i + 1}. {order.Items[i].Name} x{order.Items[i].Quantity}");

                    int sel = ReadIntInRange("Remove which index (1-based): ", 1, order.Items.Count) - 1;
                    var removed = order.Items[sel];
                    var orig = items.FirstOrDefault(x => x.ItemID == removed.ItemID);
                    if (orig != null) orig.Quantity += removed.Quantity;
                    order.Items.RemoveAt(sel);
                    Console.WriteLine("Removed from order.");
                    Pause();
                }
                else 
                {
                    if (!order.Items.Any()) { Console.WriteLine("Order is empty."); Pause(); continue; }
                    orders.Add(order);
                    SaveOrders();
                    SaveItems();
                    Console.WriteLine("\nOrder created successfully!");
                    Console.WriteLine($"Subtotal: P{order.Subtotal():F2}");
                    Console.WriteLine($"Discount: -P{order.Discount():F2}");
                    Console.WriteLine($"Tax: +P{order.Tax():F2}");
                    Console.WriteLine($"TOTAL: P{order.Total():F2}");
                    Pause();
                    return;
                }
            }
        }

        public void ViewOrdersList()
        {
            Console.Clear();
            Console.WriteLine("=== Orders ===");
            if (!orders.Any()) { Console.WriteLine("No orders."); Pause(); return; }

            Console.WriteLine("{0,-8}{1,-20}{2,-12}{3,12}", "OrderID", "Customer", "Date", "Total");
            Console.WriteLine(new string('-', 60));
            foreach (var o in orders.OrderBy(o => o.OrderID))
                Console.WriteLine("{0,-8}{1,-20}{2,-12}{3,12:F2}", o.OrderID, Trim(o.CustomerName, 20), o.Date.ToShortDateString(), o.Total());

            int id = ReadIntInRange("\nEnter Order ID to view (0 cancel): ", 0, int.MaxValue);
            if (id == 0) return;
            var ord = orders.FirstOrDefault(x => x.OrderID == id);
            if (ord == null) { Console.WriteLine("Order not found."); Pause(); return; }

            Console.Clear();
            PrintOrderReceipt(ord);
            Pause();
        }

        private static string Trim(string s, int len) => s.Length <= len ? s : s.Substring(0, len - 3) + "...";
        private static void PrintOrderReceipt(Order o)
        {
            Console.WriteLine("========================================");
            Console.WriteLine($"ALMONIA BAKERY - Order #{o.OrderID}");
            Console.WriteLine("========================================");
            Console.WriteLine($"Customer: {o.CustomerName}");
            Console.WriteLine($"Date: {o.Date}");
            Console.WriteLine("----------------------------------------");
            Console.WriteLine("{0,-20}{1,5}{2,10}{3,10}", "Item", "Qty", "Price", "Total");
            Console.WriteLine("----------------------------------------");
            foreach (var it in o.Items)
                Console.WriteLine("{0,-20}{1,5}{2,10:F2}{3,10:F2}", it.Name, it.Quantity, it.Price, it.Price * it.Quantity);
            Console.WriteLine("----------------------------------------");
            Console.WriteLine($"Subtotal: {o.Subtotal(),30:F2}");
            Console.WriteLine($"Discount: -{o.Discount():F2}");
            Console.WriteLine($"Tax: +{o.Tax():F2}");
            Console.WriteLine("----------------------------------------");
            Console.WriteLine($"TOTAL: {o.Total(),30:F2}");
            Console.WriteLine("========================================");
        }

        // ---------------- yearly Reportss ----------------
        public void GenerateSalesReport()
        {
            Console.Clear();
            if (!orders.Any()) { Console.WriteLine("No orders recorded."); Pause(); return; }

            Console.WriteLine("Generate Report");
            Console.WriteLine("1) Weekly");
            Console.WriteLine("2) Monthly");
            Console.WriteLine("3) Yearly");
            Console.WriteLine("0) Cancel");
            int ch = ReadIntInRange("> ", 0, 3);
            if (ch == 0) return;

            
            IEnumerable<Order> range = Enumerable.Empty<Order>();
            if (ch == 1)
            {
                Console.Write("Enter week start date (YYYY-MM-DD): ");
                if (!DateTime.TryParse(Console.ReadLine(), out DateTime start))
                {
                    Console.WriteLine("Invalid date."); Pause(); return;
                }
                DateTime end = start.AddDays(7);
                range = orders.Where(o => o.Date >= start && o.Date < end);
            }
            else if (ch == 2)
            {
                int month = ReadMonth("Enter month (1-12): ");
                int year = ReadYear("Enter year (e.g., 2025): ");
                range = orders.Where(o => o.Date.Month == month && o.Date.Year == year);
            }
            else if (ch == 3)
            {
                int y = ReadYear("Enter year (e.g., 2025): ");
                range = orders.Where(o => o.Date.Year == y);
            }

            if (!range.Any()) { Console.WriteLine("No orders in period."); Pause(); return; }

            Console.Clear();
            Console.WriteLine("=== SALES REPORT ===");

            
            Console.WriteLine("\n-- Order Summary --");
            Console.WriteLine("{0,-8}{1,-12}{2,-20}{3,10}{4,12}", "OrderID", "Date", "Customer", "Items", "Total(P)");
            Console.WriteLine(new string('-', 70));
            foreach (var o in range.OrderBy(o => o.OrderID))
            {
                Console.WriteLine("{0,-8}{1,-12}{2,-20}{3,10}{4,12:F2}",
                    o.OrderID, o.Date.ToShortDateString(), Trim(o.CustomerName, 20), o.Items.Sum(i => i.Quantity), o.Total());
            }

           
            Console.WriteLine("\n-- Item Summary --");
            Console.WriteLine("{0,-30}{1,10}{2,14}", "Item", "QtySold", "Sales (P)");
            Console.WriteLine(new string('-', 60));

            var itemSummary = range
                .SelectMany(o => o.Items)
                .GroupBy(i => i.Name)
                .Select(g => new { Name = g.Key, Qty = g.Sum(x => x.Quantity), Sales = g.Sum(x => x.Quantity * x.Price) })
                .OrderByDescending(x => x.Sales);

            foreach (var s in itemSummary)
                Console.WriteLine("{0,-30}{1,10}{2,14:F2}", Trim(s.Name, 30), s.Qty, s.Sales);

            Console.WriteLine("\nTotal Sales: P" + range.Sum(o => o.Total()).ToString("F2"));

           
            var top = itemSummary.FirstOrDefault();
            if (top != null)
                Console.WriteLine($"Top seller: {top.Name} — {top.Qty} pcs, P{top.Sales:F2}");

            Pause();
        }

        private int ReadYear(string prompt)
        {
            while (true)
            {
                Console.Write(prompt);
                if (int.TryParse(Console.ReadLine(), out int y) && y >= 2000 && y <= 2100)
                    return y;
                Console.WriteLine("Enter a valid year (2000–2100).");
            }
        }

        private int ReadMonth(string prompt)
        {
            while (true)
            {
                Console.Write(prompt);
                if (int.TryParse(Console.ReadLine(), out int m) && m >= 1 && m <= 12)
                    return m;
                Console.WriteLine("Enter month 1–12.");
            }
        }


        private void SaveItems()
        {
            var lines = items.Select(i => string.Join(",", 
                i.ItemID, 
                FileManager.Escape(i.Name), 
                i.Price.ToString("F2"), 
                i.Quantity));
            FileManager.WriteAllLinesSafe(ITEMS_FILE, lines);
        }

        private void LoadItems()
        {
            items.Clear();
            var lines = FileManager.ReadAllLinesSafe(ITEMS_FILE);
            foreach (var line in lines)
            {
                try
                {
                    var parts = FileManager.SplitCsvLine(line);
                    if (parts.Length < 4) continue;
                    int id = int.Parse(parts[0]);
                    string name = parts[1];
                    double price = double.Parse(parts[2]);
                    int qty = int.Parse(parts[3]);
                    items.Add(new BakeryItem(id, name, price, qty));
                }
                catch
                {
                    
                    continue;
                }
            }
            if (items.Any()) nextItemID = items.Max(i => i.ItemID) + 1;
        }

        private void SaveOrders()
        {
            var lines = new List<string>();
            foreach (var o in orders)
            {
                foreach (var it in o.Items)
                {
                    lines.Add(string.Join(",",
                        o.OrderID,
                        FileManager.Escape(o.CustomerName),
                        o.DiscountRate.ToString("F4"),
                        o.Date.ToString("o"),
                        FileManager.Escape(it.Name),
                        it.Quantity,
                        it.Price.ToString("F2")));
                }
            }
            FileManager.WriteAllLinesSafe(ORDERS_FILE, lines);
        }

        private void LoadOrders()
        {
            orders.Clear();
            var lines = FileManager.ReadAllLinesSafe(ORDERS_FILE);
            foreach (var line in lines)
            {
                try
                {
                    var parts = FileManager.SplitCsvLine(line);
                    if (parts.Length < 7) continue;
                    int orderId = int.Parse(parts[0]);
                    string cust = parts[1];
                    double discount = double.Parse(parts[2]);
                    DateTime date = DateTime.Parse(parts[3]);
                    string itemName = parts[4];
                    int qty = int.Parse(parts[5]);
                    double price = double.Parse(parts[6]);

                    var existing = orders.FirstOrDefault(o => o.OrderID == orderId);
                    if (existing == null)
                    {
                        existing = new Order(orderId, cust, discount) { Date = date };
                        orders.Add(existing);
                    }
                    existing.Items.Add(new BakeryItem(0, itemName, price, qty));
                }
                catch
                {
                    
                    continue;
                }
            }
            if (orders.Any()) nextOrderID = orders.Max(o => o.OrderID) + 1;
        }

        
        private string ReadStringWithCancel(string prompt)
        {
            while (true)
            {
                Console.Write(prompt);
                string s = Console.ReadLine();
                if (s == null) return null;
                if (s.Trim().ToUpper() == "CANCEL") return null;
                if (!string.IsNullOrWhiteSpace(s)) return s.Trim();
                Console.WriteLine("Input cannot be empty.");
            }
        }

        private double? ReadDoubleWithCancel(string prompt)
        {
            while (true)
            {
                Console.Write(prompt);
                string s = Console.ReadLine();
                if (s == null) return null;
                if (s.Trim().ToUpper() == "CANCEL") return null;
                if (double.TryParse(s, out double val) && val >= 0) return val;
                Console.WriteLine("Enter a non-negative number.");
            }
        }

        private int? ReadIntWithCancel(string prompt)
        {
            while (true)
            {
                Console.Write(prompt);
                string s = Console.ReadLine();
                if (s == null) return null;
                if (s.Trim().ToUpper() == "CANCEL") return null;
                if (int.TryParse(s, out int val) && val >= 0) return val;
                Console.WriteLine("Enter 0 or a positive integer.");
            }
        }
    }

    
    public static class ProgramUI
    {
        private const string MANAGER_PASSWORD = "manager123"; 

        public static void Main()
        {
            var system = new BakerySystem();

            while (true)
            {
                Console.Clear();
                Console.WriteLine("=== ALMONIA BAKERY ===");
                Console.WriteLine("Select role:");
                Console.WriteLine("1. Manager");
                Console.WriteLine("2. Worker");
                Console.WriteLine("0. Exit");
                Console.Write("Choose: ");

                string r = Console.ReadLine();
                if (r == "0") return;
                else if (r == "1")
                {
                    if (AuthenticateManager())
                    {
                        ManagerMenu(system);
                    }
                    else
                    {
                        Console.WriteLine("Access denied. Returning to main menu.");
                        Pause();
                    }
                }
                else if (r == "2")
                {
                    WorkerMenu(system);
                }
                else
                {
                    Console.WriteLine("Invalid choice.");
                    Pause();
                }
            }
        }

        private static bool AuthenticateManager()
        {
            int attempts = 0;
            const int maxAttempts = 3;

            while (attempts < maxAttempts)
            {
                Console.Clear();
                Console.WriteLine("=== MANAGER AUTHENTICATION ===");
                Console.WriteLine($"Attempts remaining: {maxAttempts - attempts}");

                string password = ReadPassword("Enter manager password: ");

                if (password == MANAGER_PASSWORD)
                {
                    return true;
                }
                else
                {
                    attempts++;
                    Console.WriteLine("Incorrect password.");
                    Pause();
                }
            }

            return false;
        }

        private static string ReadPassword(string prompt)
        {
            Console.Write(prompt);
            string password = "";
            ConsoleKeyInfo key;

            do
            {
                key = Console.ReadKey(true);

                if (key.Key != ConsoleKey.Backspace && key.Key != ConsoleKey.Enter)
                {
                    password += key.KeyChar;
                    Console.Write("*");
                }
                else if (key.Key == ConsoleKey.Backspace && password.Length > 0)
                {
                    password = password.Substring(0, password.Length - 1);
                    Console.Write("\b \b");
                }
            } while (key.Key != ConsoleKey.Enter);

            Console.WriteLine();
            return password;
        }

        private static void ManagerMenu(BakerySystem system)
        {
            while (true)
            {
                Console.Clear();
                Console.WriteLine("=== MANAGER MENU ===");
                Console.WriteLine("1. Add Item");
                Console.WriteLine("2. Edit Item");
                Console.WriteLine("3. Update Stock");
                Console.WriteLine("4. Remove Item");
                Console.WriteLine("5. View Items");
                Console.WriteLine("6. Search Items");
                Console.WriteLine("7. Create Order");
                Console.WriteLine("8. View Orders");
                Console.WriteLine("9. Generate Sales Report");
                Console.WriteLine("0. Log out");
                int choice = BakerySystem_ReadInt("Choose: ", 0, 9);
                switch (choice)
                {
                    case 1: system.AddItem(); break;
                    case 2: system.EditItem(); break;
                    case 3: system.UpdateStock(); break;
                    case 4: system.RemoveItem(); break;
                    case 5: system.ViewItemsTable(); Pause(); break;
                    case 6: system.SearchItems(); break;
                    case 7: system.CreateOrder(); break;
                    case 8: system.ViewOrdersList(); break;
                    case 9: system.GenerateSalesReport(); break;
                    case 0: return;
                }
            }
        }

        private static void WorkerMenu(BakerySystem system)
        {
            while (true)
            {
                Console.Clear();
                Console.WriteLine("=== WORKER MENU ===");
                Console.WriteLine("1. View Items");
                Console.WriteLine("2. Search Items");
                Console.WriteLine("3. Create Order");
                Console.WriteLine("4. View Orders");
                Console.WriteLine("0. Log out");
                int choice = BakerySystem_ReadInt("Choose: ", 0, 4);
                switch (choice)
                {
                    case 1: system.ViewItemsTable(); Pause(); break;
                    case 2: system.SearchItems(); break;
                    case 3: system.CreateOrder(); break;
                    case 4: system.ViewOrdersList(); break;
                    case 0: return;
                }
            }
        }

        private static int BakerySystem_ReadInt(string prompt, int min, int max)
        {
            while (true)
            {
                Console.Write(prompt);
                if (int.TryParse(Console.ReadLine(), out int v) && v >= min && v <= max) return v;
                Console.WriteLine($"Enter integer between {min} and {max}.");
            }
        }

        private static void Pause()
        {
            Console.WriteLine("\nPress ENTER to continue...");
            Console.ReadLine();
        }
    }
}