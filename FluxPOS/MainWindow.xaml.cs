using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using SQLite; //herramienta para la base de datos

namespace FluxPOS
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // definimos el nombre y ubicacion del archivo de bd
        string rutaBaseDeDatos = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "FluxPOS.db");

        //Definir la lista
        List<Producto> listaDeProductos = new List<Producto>();
        //Lista para productos que el cliente va comprando en el momento
        List<Producto> listaCarrito = new List<Producto>();
        public MainWindow()
        {
            InitializeComponent();
            // Se crea la conexion y la tabla si no existen
            using (SQLiteConnection conexion = new SQLiteConnection(rutaBaseDeDatos))
            {
                conexion.CreateTable<Producto>(); //crea una tabla basada en el molde de Producto
            }
            CargarProductos();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (decimal.TryParse(txtPrecio.Text, out decimal precioFinal))
            {
                //1- crea un nuevo objeto usando el "molde" de la clase Producto
                Producto nuevoProducto = new Producto
                {   //se asignan los datos de los Texbox a sus propiedades y parseo el string de precio en decimal
                    Nombre = txtNombre.Text,
                    Precio = precioFinal
                };

                //2-se abre la conexion y guarda en la bd (persistencia)
                using (SQLiteConnection conexion = new SQLiteConnection(rutaBaseDeDatos))
                {
                    conexion.Insert(nuevoProducto);
                }

                //3- Actualiza la interfaz
                listaDeProductos.Add(nuevoProducto);
                lstProductos.Items.Add(nuevoProducto.Nombre + " - $" + nuevoProducto.Precio);

                //4- se llama al metodo actualizar total
                ActualizarTotal();

                //5- Limpiamos los cuadros de texto para el siguiente ingreso
                txtNombre.Clear();
                txtPrecio.Clear();
            }
            else
            {
                MessageBox.Show("Por favor, ingresa un precio númerico válido.");
            }
        }
        private void ActualizarTotal()
        {
            decimal suma = 0;
            //se recorre la lista uno por uno
            foreach (Producto p in listaDeProductos)
            {
                suma = suma + p.Precio; //se acumula el precio de cada producto
            }
            //Se muestra el resultado de la acumulacion en la interfaz
            lblTotalInventario.Text = "Valor del Inventario: $" + suma.ToString("N2");
        }
        private void CargarProductos()
        {
            using (SQLiteConnection conexion = new SQLiteConnection(rutaBaseDeDatos))
            {
                //Se pide a SQLite todos los productos de la tabla
                var productosDeBaseDeDatos = conexion.Table<Producto>().ToList();

                //se limpia la lista de la pantalla por seguridad
                lstProductos.Items.Clear();
                listaDeProductos.Clear();

                foreach (var p in productosDeBaseDeDatos)
                {
                    listaDeProductos.Add(p);
                    lstProductos.Items.Add($"{p.Nombre} - ${p.Precio:N2}");
                }
            }
            ActualizarTotal();
        }

        private void btnEliminar_Click(object sender, EventArgs e)
        {
            //1- verifica si hay algo seleccionado en el listBox
            if (lstProductos.SelectedIndex != -1)
            {
                //2- se obtiene el producto de nuetra lista de memoria usando el indice seleccionado
                int indice = lstProductos.SelectedIndex;
                Producto productoABorrar = listaDeProductos[indice];

                //3- lo borramos de la base de datos usando su ID
                using (SQLiteConnection conexion = new SQLiteConnection(rutaBaseDeDatos))
                {
                    //Delete necesita saber que clase es el objeto y cual es su id
                    conexion.Delete(productoABorrar);
                }
                //4 mensaje de confirmacion
                MessageBox.Show($"Producto '{productoABorrar.Nombre}' eliminado correctamente.");
                //5 refresca la pantalla cargando todo de nuevo desde la BD
                CargarProductos();
            }
            else
            {
                MessageBox.Show("Por favor, selecciona un producto de la lista para eliminar.");
            }
        }

        private void lstProductos_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            //verifico que haya algo seleccionado
            if (lstProductos.SelectedIndex != -1)
            {
                //obtengo el producto seleccionado de la lista de inventario
                Producto seleccionado = listaDeProductos[lstProductos.SelectedIndex];

                //lo agrego a la lista del carrito (en la RAM)
                listaCarrito.Add(seleccionado);

                //lo muestro en la list box de la derecha
                lstCarrito.Items.Add($"{seleccionado.Nombre} - ${seleccionado.Precio:N2}");

                //actualizo el total de la venta actual
                ActualizarTotalVenta();
            }
        }

        private void ActualizarTotalVenta()
        {
            decimal sumaVenta = 0;
            foreach(Producto p in listaCarrito)
            {
                sumaVenta += p.Precio;
            }
            // lblTotal es el nombre del Textblock que esta en la zona del carrito
            lblTotal.Text = "Total Venta: $" + sumaVenta.ToString("N2");
        }

        public class Producto
        {
            [PrimaryKey, AutoIncrement] //le dice a SQlite que no hay dos productos con el mismo ID y que lo haga autoincrementado
            public int Id { get; set; } //crear un id unico por producto

            public string Nombre { get; set; }
            public decimal Precio { get; set; }
        }
    }
}