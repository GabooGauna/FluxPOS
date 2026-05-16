using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Input;
using SQLite;
using FluxPOS.Models; // Conexión obligatoria con la nueva carpeta de modelos 
namespace FluxPOS
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // Definimos la ubicación del archivo de la base de datos de manera segura y fija 
        private readonly string rutaBaseDeDatos = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "FluxPOS.db"
        );

        // Listas de memoria RAM protegidas con readonly [cite: 825]
        private readonly List<Producto> listaDeProductos = new List<Producto>();
        private readonly List<Producto> listaCarrito = new List<Producto>();

        public MainWindow()
        {
            InitializeComponent();

            // Se crea la conexión y la tabla si no existen al iniciar la app 
            using (SQLiteConnection conexion = new SQLiteConnection(rutaBaseDeDatos))
            {
                conexion.CreateTable<Producto>();
            }

            CargarProductos();
        }

        #region EVENTOS DE LA INTERFAZ DE USUARIO (UI)

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (decimal.TryParse(txtPrecio.Text, out decimal precioFinal))
            {
                // Creación del objeto Producto utilizando la sintaxis moderna 
                Producto nuevoProducto = new Producto
                {
                    Nombre = txtNombre.Text,
                    Precio = precioFinal
                };

                // Persistencia: Guardado físico en SQLite [cite: 190]
                using (SQLiteConnection conexion = new SQLiteConnection(rutaBaseDeDatos))
                {
                    conexion.Insert(nuevoProducto);
                }

                // Actualización inmediata de la interfaz de usuario 
                listaDeProductos.Add(nuevoProducto);
                lstProductos.Items.Add($"{nuevoProducto.Nombre} - ${nuevoProducto.Precio:N2}");

                ActualizarTotal();

                // Limpieza de campos para el siguiente registro 
                txtNombre.Clear();
                txtPrecio.Clear();
            }
            else
            {
                MessageBox.Show("Por favor, ingresa un precio numérico válido.");
            }
        }

        // CORRECCIÓN: Cambiado 'EventArgs' por 'RoutedEventArgs' para compatibilidad con WPF 
        private void btnEliminar_Click(object sender, RoutedEventArgs e)
        {
            if (lstProductos.SelectedIndex != -1)
            {
                int indice = lstProductos.SelectedIndex;
                Producto productoABorrar = listaDeProductos[indice];

                using (SQLiteConnection conexion = new SQLiteConnection(rutaBaseDeDatos))
                {
                    conexion.Delete(productoABorrar);
                }

                MessageBox.Show($"Producto '{productoABorrar.Nombre}' eliminado correctamente.");
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

        #endregion

        #region MÉTODOS DE LÓGICA Y CÁLCULO

        private void CargarProductos()
        {
            using (SQLiteConnection conexion = new SQLiteConnection(rutaBaseDeDatos))
            {
                var productosDeBaseDeDatos = conexion.Table<Producto>().ToList();

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

        private void ActualizarTotal()
        {
            decimal suma = 0;
            foreach (Producto p in listaDeProductos)
            {
                suma += p.Precio;
            }
            lblTotalInventario.Text = "Valor del Inventario: $" + suma.ToString("N2");
        }

        private void ActualizarTotalVenta()
        {
            decimal sumaVenta = 0;
            foreach (Producto p in listaCarrito)
            {
                sumaVenta += p.Precio;
            }
            // lblTotal es el nombre del Textblock que esta en la zona del carrito
            lblTotal.Text = "Total Venta: $" + sumaVenta.ToString("N2");
        }

#endregion
    }
}