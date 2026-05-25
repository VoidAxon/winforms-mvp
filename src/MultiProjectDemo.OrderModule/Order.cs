namespace MultiProjectDemo.OrderModule
{
    public class Order
    {
        public int Id { get; set; }
        public int CustomerUserId { get; set; }
        public decimal Amount { get; set; }
        public string Description { get; set; }
    }
}
