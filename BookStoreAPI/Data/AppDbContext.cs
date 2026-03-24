using Microsoft.EntityFrameworkCore;
using BookStoreAPI.Models;

namespace BookStoreAPI.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Account> Accounts { get; set; }
        public DbSet<Admin> Admins { get; set; }
        public DbSet<Customer> Customers { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<Book> Books { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderItem> OrderItems { get; set; }
        public DbSet<CartItem> CartItems { get; set; }
        public DbSet<Review> Reviews { get; set; }

        protected override void OnModelCreating(ModelBuilder mb)
        {

            // Account - Customer (1-1)
            mb.Entity<Customer>()
              .HasOne(c => c.Account)
              .WithOne(a => a.Customer)
              .HasForeignKey<Customer>(c => c.accountID)
              .OnDelete(DeleteBehavior.Cascade);

            // Account - Admin (1-1)
            mb.Entity<Admin>()
              .HasOne(a => a.Account)
              .WithOne(ac => ac.Admin)
              .HasForeignKey<Admin>(a => a.accountID)
              .OnDelete(DeleteBehavior.Cascade);

            // Book - Category (N-1, SET NULL)
            mb.Entity<Book>()
              .HasOne(b => b.Category)
              .WithMany(c => c.Books)
              .HasForeignKey(b => b.categoryID)
              .OnDelete(DeleteBehavior.SetNull);

            // Order - Customer (N-1, RESTRICT) 
            mb.Entity<Order>()
              .HasOne(o => o.Customer)
              .WithMany(c => c.Orders)
              .HasForeignKey(o => o.userID)
              .OnDelete(DeleteBehavior.Restrict);

            // OrderItem - Order (N-1, CASCADE)
            mb.Entity<OrderItem>()
              .HasOne(oi => oi.Order)
              .WithMany(o => o.OrderItems)
              .HasForeignKey(oi => oi.orderID)
              .OnDelete(DeleteBehavior.Cascade);

            // OrderItem - Book (N-1, RESTRICT) 
            mb.Entity<OrderItem>()
              .HasOne(oi => oi.Book)
              .WithMany(b => b.OrderItems)
              .HasForeignKey(oi => oi.bookID)
              .OnDelete(DeleteBehavior.Restrict);

            // Review - Customer (N-1, CASCADE)
            mb.Entity<Review>()
              .HasOne(r => r.Customer)
              .WithMany(c => c.Reviews)
              .HasForeignKey(r => r.userID)
              .OnDelete(DeleteBehavior.Cascade);

            // Review - Book (N-1, CASCADE)
            mb.Entity<Review>()
              .HasOne(r => r.Book)
              .WithMany(b => b.Reviews)
              .HasForeignKey(r => r.bookID)
              .OnDelete(DeleteBehavior.Cascade);

            // Unique: 1 user chỉ review 1 lần / sácH
            mb.Entity<Review>()
              .HasIndex(r => new { r.userID, r.bookID })
              .IsUnique();

            // CartItem - Customer (N-1, CASCADE)
            mb.Entity<CartItem>()
              .HasOne(c => c.Customer)
              .WithMany()
              .HasForeignKey(c => c.userID)
              .OnDelete(DeleteBehavior.Cascade);

            // CartItem - Book (N-1, CASCADE)
            mb.Entity<CartItem>()
              .HasOne(c => c.Book)
              .WithMany()
              .HasForeignKey(c => c.bookID)
              .OnDelete(DeleteBehavior.Cascade);

            // 1 user chỉ có một giỏ hàng duy nhất cho mỗi sách
            mb.Entity<CartItem>()
              .HasIndex(c => new { c.userID, c.bookID })
              .IsUnique();
        }
    }
}