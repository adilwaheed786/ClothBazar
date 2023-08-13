namespace ClothBazar.Database.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class OrderItemAdded : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.OrderItems", "ItemPrice", c => c.Decimal(nullable: false, precision: 18, scale: 2));
        }
        
        public override void Down()
        {
            DropColumn("dbo.OrderItems", "ItemPrice");
        }
    }
}
