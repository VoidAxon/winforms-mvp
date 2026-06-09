namespace WinformsMVP.Samples.CascadeDemo
{
    public static class CascadeDemoProgram
    {
        public static void Run()
        {
            using (var form = new CascadeForm())
            {
                form.ShowDialog();
            }
        }
    }
}
