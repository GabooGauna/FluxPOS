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

        // Listas de memoria RAM protegidas con readonly 
        private readonly List<Producto> listaDeProductos = new List<Producto>();
        private readonly List<Producto> listaCarrito = new List<Producto>();

        public MainWindow()
        {
            InitializeComponent();

            // Se crea la conexión y la tabla si no existen al iniciar la app 
            using (SQLiteConnection conexion = new SQLiteConnection(rutaBaseDeDatos))
            {
                conexion.CreateTable<Producto>(); //tabla inventario
                conexion.CreateTable<Venta>(); //tabla para historial de ventas. Tickets (maestro)
                conexion.CreateTable<DetalleVenta>(); //renglones de Productos (tabla intermedia)
            }

            CargarProductos();
        }

        #region EVENTOS DE LA INTERFAZ DE USUARIO (UI)

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            //se valida que el precio final sea decimal y el stock un numero entero
            if (decimal.TryParse(txtPrecio.Text, out decimal precioFinal))
            {
                if (int.TryParse(txtStock.Text, out int stockFinal))
                {
                    // Creación del objeto Producto incluyendo la prop stock
                    Producto nuevoProducto = new Producto
                    {
                        Nombre = txtNombre.Text,
                        Precio = precioFinal,
                        Stock = stockFinal //se guarda el stock inicial
                    };

                    // Persistencia: Guardado físico en SQLite 
                    using (SQLiteConnection conexion = new SQLiteConnection(rutaBaseDeDatos))
                    {
                        conexion.Insert(nuevoProducto);
                    }

                    // Actualización inmediata de la interfaz de usuario mostrando las unidades disponibles 
                    listaDeProductos.Add(nuevoProducto);
                    lstProductos.Items.Add($"{nuevoProducto.Nombre} - ${nuevoProducto.Precio:N2} [Stock: {nuevoProducto.Stock}]");

                    ActualizarTotal();

                    // Limpieza de campos para el siguiente registro 
                    txtNombre.Clear();
                    txtPrecio.Clear();
                    txtStock.Clear();
                }
                else
                {
                    MessageBox.Show("Por favor, ingresa una cantidad de stock entera y válida");
                }
            }
            else
            {
                MessageBox.Show("Por favor, ingresa un precio numérico válido.");
            }
        }

        private void btnRestarStock_Click(object sender, RoutedEventArgs e)
        {
            //verifica seleccion en el inventario
            if (lstProductos.SelectedIndex != -1)
            {
                int indice = lstProductos.SelectedIndex;
                Producto productoAEditar = listaDeProductos[indice];

                //solo resta si el stock es mayor a 0
                if(productoAEditar.Stock > 0)
                {
                    productoAEditar.Stock -= 1; //resta una unidad fisica
                    using(SQLiteConnection conexion = new SQLiteConnection(rutaBaseDeDatos))
                    {
                        //se guarda el cambio permanentemente en el disco
                        conexion.Update(productoAEditar);
                    }

                    MessageBox.Show($"Ajuste de Stock: Se descontó 1 unidad de '{productoAEditar.Nombre}'. Nuevo Stock: {productoAEditar.Stock}");
                    //refresca la pantalla y recalcula el valor de inventario
                    CargarProductos();
                }
                else
                {
                    MessageBox.Show($"El producto '{productoAEditar.Nombre}' ya se encuentra en 0 unidades. No es posible restar más.");
                }
            }
            else
            {
                MessageBox.Show("Por favor, selecciona un producto del inventario para restar unidades.");
            }
        }

        private void btnEliminar_Click(object sender, RoutedEventArgs e)
        {
            if (lstProductos.SelectedIndex != -1)
            {
                int indice = lstProductos.SelectedIndex;
                Producto productoABorrar = listaDeProductos[indice];

                //seguridad: confirmacion obligatoria antes de romper la bd
                MessageBoxResult respuesta = MessageBox.Show(
                        $"¿Está seguro de eliminar '{productoABorrar.Nombre}'?\nEsta acción lo borrará permanentemente de tu catálogo y del historial del inventario.",
                        "ADVERTENCIA CRÍTICA",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning
                );

                if(respuesta == MessageBoxResult.Yes)
                {
                    using (SQLiteConnection conexion = new SQLiteConnection(rutaBaseDeDatos))
                    {
                        conexion.Delete(productoABorrar);
                    }

                    MessageBox.Show($"El artículo '{productoABorrar.Nombre}' fue removito exitosamente del sistema.");
                    CargarProductos();
                }
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

        private void btnFinalizarCompra_Click(object sender, RoutedEventArgs e)
        {
            //validar que haya algo en el carrito
            if(listaCarrito.Count == 0)
            {
                MessageBox.Show("El carrito esta vacío. Añade productos antes de finalizar.");
                return; //detiene la ejecucion del metodo
            }

            //calcula el total recorriendo los elementos actuales del carrito
            decimal totalVenta = 0;
            foreach(Producto p in listaCarrito)
            {
                totalVenta += p.Precio;
            }

            //fabrica el objeto venta (ticket) con los datos listos
            Venta nuevaVenta = new Venta
            {
                Fecha = DateTime.Now, //captura la fecha y hora actual de la computadora
                Total = totalVenta
            };

            //se abre una conexion segura y guarda fisicamente en el disco duro
            using (SQLiteConnection conexion = new SQLiteConnection(rutaBaseDeDatos))
            {
                conexion.Insert(nuevaVenta);
                /*procesar el stock y los detalles
                para no repetir el mismo producto, se crea un diccionario temporal en la RAM
                teniendo como clave la ID del Producto, el valor va a ser un objeto DetalleVenta agrupado*/
                Dictionary<int, DetalleVenta> detallesAgrupados = new Dictionary<int, DetalleVenta>();

                foreach (Producto p in listaCarrito)
                {
                    if (detallesAgrupados.ContainsKey(p.Id))
                    {
                        //si el producto ya aparecio en el carrito, le sumamos 1 a la cantidad de ese renglon
                        detallesAgrupados[p.Id].Cantidad += 1;
                    }
                    else
                    {
                        //si es la primera vez que aparece el producto en el ticket, fabrica su renglon historico
                        DetalleVenta nuevoRenglon = new DetalleVenta
                        {
                            VentaId = nuevaVenta.Id, //enlaza el renglon con el ID del ticket recien generado
                            ProductoId = p.Id,
                            NombreProducto = p.Nombre,
                            Cantidad = 1, //inicia con la primera unidad
                            PrecioCobrado = p.Precio //congela el precio actual por seguridad historica
                        };
                        detallesAgrupados.Add(p.Id, nuevoRenglon);
                    }
                }

                //ahora recorre los detalles agrupados para impactar fisicamente la db
                foreach(var par in detallesAgrupados)
                {
                    DetalleVenta detalleAFijar = par.Value;
                    // a) graba de forma persistente el renglón en la tabla intermedia de SQLite
                    conexion.Insert(detalleAFijar);

                    // b) ALGORITMO DE DESCUENTO: busca el producto real en el inventario físico mediante su ID 
                    Producto productoEnInventario = conexion.Table<Producto>().FirstOrDefault(x => x.Id == detalleAFijar.ProductoId);

                    if(productoEnInventario != null)
                    {
                        //resta las unidades que el cliente compro del stock actual en la bd
                        productoEnInventario.Stock -= detalleAFijar.Cantidad;

                        //actualiza el producto con su nuevo stock modificado
                        conexion.Update(productoEnInventario);
                    }
                }
            }

            //avisa al usuario del exito de la transaccion
            MessageBox.Show($"¡Venta realizada con éxito!\nTicket N°: {nuevaVenta.Id}\nTotal Cobrado: ${totalVenta:N2}\n\nEl stock ha sido actualizado correctamente.");

            //limpia el carrito (RAM e interfaz) para la proxima venta
            listaCarrito.Clear();
            lstCarrito.Items.Clear();

            //refresca la lista izquierda para ver el nuevo stock y recalcula totales
            CargarProductos();
            ActualizarTotalVenta(); //reinicia el contador a 0
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
                    lstProductos.Items.Add($"{p.Nombre} - ${p.Precio:N2} [Stock: {p.Stock}]");
                }
            }
            ActualizarTotal();
        }

        private void ActualizarTotal()
        {
            decimal sumaTotalInventario = 0;
            foreach (Producto p in listaDeProductos)
            {
                sumaTotalInventario += (p.Precio * p.Stock);
            }
            lblTotalInventario.Text = "Valor del Inventario: $" + sumaTotalInventario.ToString("N2");
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