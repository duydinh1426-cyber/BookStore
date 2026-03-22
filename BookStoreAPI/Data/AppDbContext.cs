using Microsoft.EntityFrameworkCore;
using BookStoreAPI.Models;

namespace BookStoreAPI.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Account> Accounts => Set<Account>();
        public DbSet<Customer> Customers => Set<Customer>();
        public DbSet<Admin> Admins => Set<Admin>();
        public DbSet<Category> Categories => Set<Category>();
        public DbSet<Book> Books => Set<Book>();
        public DbSet<Order> Orders => Set<Order>();
        public DbSet<OrderItem> OrderItems => Set<OrderItem>();
        public DbSet<Review> Reviews => Set<Review>();
        public DbSet<CartItem> CartItems => Set<CartItem>(); // ← thêm mới

        protected override void OnModelCreating(ModelBuilder mb)
        {
            mb.Entity<Account>().ToTable("Accounts");
            mb.Entity<Admin>().ToTable("Admin");
            mb.Entity<Customer>().ToTable("Customers");
            mb.Entity<Category>().ToTable("Categories");
            mb.Entity<Book>().ToTable("Books");
            mb.Entity<Order>().ToTable("Orders");
            mb.Entity<OrderItem>().ToTable("OrderItems");
            mb.Entity<Review>().ToTable("Reviews");
            mb.Entity<CartItem>().ToTable("CartItems");

            // ── Account → Customer (1-1) ──────────────────────────────
            mb.Entity<Customer>()
              .HasOne(c => c.Account)
              .WithOne(a => a.Customer)
              .HasForeignKey<Customer>(c => c.accountID)
              .OnDelete(DeleteBehavior.Cascade);

            // ── Account → Admin (1-1) ─────────────────────────────────
            mb.Entity<Admin>()
              .HasOne(a => a.Account)
              .WithOne(ac => ac.Admin)
              .HasForeignKey<Admin>(a => a.accountID)
              .OnDelete(DeleteBehavior.Cascade);

            // ── Book → Category ───────────────────────────────────────
            mb.Entity<Book>()
              .HasOne(b => b.Category)
              .WithMany(c => c.Books)
              .HasForeignKey(b => b.categoryID)
              .OnDelete(DeleteBehavior.SetNull);

            // ── Order → Customer ──────────────────────────────────────
            mb.Entity<Order>()
              .HasOne(o => o.Customer)
              .WithMany(c => c.Orders)
              .HasForeignKey(o => o.userID)
              .OnDelete(DeleteBehavior.Restrict);

            // ── OrderItem → Order ─────────────────────────────────────
            mb.Entity<OrderItem>()
              .HasOne(oi => oi.Order)
              .WithMany(o => o.OrderItems)
              .HasForeignKey(oi => oi.orderID)
              .OnDelete(DeleteBehavior.Cascade);

            // ── OrderItem → Book ──────────────────────────────────────
            mb.Entity<OrderItem>()
              .HasOne(oi => oi.Book)
              .WithMany(b => b.OrderItems)
              .HasForeignKey(oi => oi.bookID)
              .OnDelete(DeleteBehavior.Restrict);

            // ── Review → Customer ─────────────────────────────────────
            mb.Entity<Review>()
              .HasOne(r => r.Customer)
              .WithMany(c => c.Reviews)
              .HasForeignKey(r => r.userID)
              .OnDelete(DeleteBehavior.Cascade);

            // ── Review → Book ─────────────────────────────────────────
            mb.Entity<Review>()
              .HasOne(r => r.Book)
              .WithMany(b => b.Reviews)
              .HasForeignKey(r => r.bookID)
              .OnDelete(DeleteBehavior.Cascade);

            // ── Unique: 1 user chỉ review 1 lần / sách ───────────────
            mb.Entity<Review>()
              .HasIndex(r => new { r.userID, r.bookID })
              .IsUnique();

            // ── CartItem → Customer ───────────────────────────────────
            mb.Entity<CartItem>()
              .HasOne(c => c.Customer)
              .WithMany()
              .HasForeignKey(c => c.userID)
              .OnDelete(DeleteBehavior.Cascade);

            // ── CartItem → Book ───────────────────────────────────────
            mb.Entity<CartItem>()
              .HasOne(c => c.Book)
              .WithMany()
              .HasForeignKey(c => c.bookID)
              .OnDelete(DeleteBehavior.Cascade);

            // ── Unique: 1 user chỉ có 1 dòng cho mỗi cuốn sách ───────
            mb.Entity<CartItem>()
              .HasIndex(c => new { c.userID, c.bookID })
              .IsUnique();

            // ── Decimal precision ─────────────────────────────────────
            mb.Entity<Book>()
              .Property(b => b.price)
              .HasColumnType("decimal(10,2)");

            mb.Entity<Order>()
              .Property(o => o.totalCost)
              .HasColumnType("decimal(10,2)");

            mb.Entity<OrderItem>()
              .Property(oi => oi.unitPrice)
              .HasColumnType("decimal(10,2)");
        }
    }
}