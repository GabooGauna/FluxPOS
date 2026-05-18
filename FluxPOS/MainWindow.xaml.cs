using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using Microsoft.VisualBasic;
using System.Windows.Controls;
using System.Windows.Input;
using FluxPOS.Models; // conexión obligatoria con la nueva carpeta de modelos 
using FluxPOS.Services; //conexión con la carpeta de servicios

namespace FluxPOS
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // instancio el servicio como unico motor de datos e inteligencia
        private readonly InventarioService _inventarioService = new InventarioService();

        // Listas de memoria RAM protegidas con readonly 
        private readonly List<Producto> listaDeProductos = new List<Producto>();
        private readonly List<Producto> listaCarrito = new List<Producto>();

        public MainWindow()
        {
            InitializeComponent();
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

                    // Persistencia: delega el guardado fisico al servicio 
                    _inventarioService.RegistrarProducto(nuevoProducto);

                    // Actualización inmediata de la interfaz de usuario mostrando las unidades disponibles 
                    listaDeProductos.Add(nuevoProducto);
                    lstProductos.Items.Add($"{nuevoProducto.Nombre} - ${nuevoProducto.Precio:N2} [Stock: {nuevoProducto.Stock}]");

                    ActualizarTotalInventario();

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
            if (lstProductosAdmin.SelectedIndex != -1)
            {
                Producto seleccionado = listaDeProductos[lstProductosAdmin.SelectedIndex];

                //solo resta si el stock es mayor a 0
                if(seleccionado.Stock > 0)
                {
                    //delega el decremento matematico y de bd al servicio
                    _inventarioService.RestarUnidadStock(seleccionado);

                    MessageBox.Show($"Ajuste de Stock: Se descontó 1 unidad de '{seleccionado.Nombre}'. Nuevo Stock: {seleccionado.Stock}");
                    //refresca la pantalla y recalcula el valor de inventario
                    CargarProductos();
                }
                else
                {
                    MessageBox.Show($"El producto '{seleccionado.Nombre}' ya se encuentra en 0 unidades. No es posible restar más.");
                }
            }
            else
            {
                MessageBox.Show("Por favor, selecciona un producto del inventario para restar unidades.");
            }
        }

        private void btnEliminar_Click(object sender, RoutedEventArgs e)
        {
            if (lstProductosAdmin.SelectedIndex != -1)
            {
                Producto seleccionado = listaDeProductos[lstProductosAdmin.SelectedIndex];

                //seguridad: confirmacion obligatoria antes de romper la bd
                MessageBoxResult respuesta = MessageBox.Show(
                        $"¿Está seguro de eliminar '{seleccionado.Nombre}'?\nEsta acción lo borrará permanentemente de tu catálogo y del historial del inventario.",
                        "ADVERTENCIA CRÍTICA",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning
                );

                if(respuesta == MessageBoxResult.Yes)
                {
                    _inventarioService.EliminarProducto(seleccionado);

                    MessageBox.Show($"El artículo '{seleccionado.Nombre}' fue removito exitosamente del sistema.");
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

            //delega la transaccion de guardado masuvo y descuento al servicio
            _inventarioService.FinalizarVenta(listaCarrito, totalVenta, out int ticketGeneradoId);

            //avisa al usuario del exito de la transaccion
            MessageBox.Show($"¡Venta realizada con éxito!\nTicket N°: {ticketGeneradoId}\nTotal Cobrado: ${totalVenta:N2}\n\nEl stock ha sido actualizado correctamente.");

            //limpia el carrito (RAM e interfaz) para la proxima venta
            listaCarrito.Clear();
            lstCarrito.Items.Clear();

            //refresca la lista izquierda para ver el nuevo stock y recalcula totales
            CargarProductos();
            ActualizarTotalVenta(); //reinicia el contador a 0
        }

        private bool _autorizado = false;
        private void tcPrincipal_SelectionChanged(Object sender, SelectionChangedEventArgs e)
        {
            //verifica si el usuario intenta hacer click en la pestaña de admin
            if(tcPrincipal.SelectedItem == tiInventario && !_autorizado)
            {
                //fuerza el regreso temporal a la pestaña de cajero para bloquear la view
                tcPrincipal.SelectedIndex = 0;
                //despliega control de acceso licito pidiendo el PIN por codigo
                //genera un inputbox para no realizar otra interfaz con otra ventana
                string passwordIngrado = Microsoft.VisualBasic.Interaction.InputBox(
                    "Ingresa la clave de Administrador para acceder al control de Stock y precios:",
                    "Acceso Protegido - FluxPOS",
                    ""
                );
                if (passwordIngrado == "1234") //clave corp fijada
                {
                    _autorizado = true; //abre cerrojo logico
                    tcPrincipal.SelectedItem = tiInventario; //mueve fisicamente a pantalla admin
                    _autorizado=false; //vuelve a cerrar el cerrojo para la prox vez
                }
                else
                {
                    MessageBox.Show("Clave incorrecta. Acceso denegado.", "Error de Autenticación", MessageBoxButton.OK, MessageBoxImage.Stop);
                }
            }
        }

        private void txtBuscar_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            //captura lo que escribe el usuario y lo pasa a minuscula
            string criterio = txtBuscar.Text.ToLower().Trim();
            //limpia visualmente la lista de la pantalla para redibujarla con el filtro
            lstProductos.Items.Clear();

            //si el buscador esta vacio no muestra nada
            if (string.IsNullOrEmpty(criterio))
            {
                return; //corta la ejecucion aca dejando la lista vacia y limpia
            }

            //Filtra la lista maestra de la RAM y busca cualquier producto que contenga el criterio escrito
            foreach(Producto p in listaDeProductos)
            {
                if (p.Nombre.ToLower().Contains(criterio))
                {
                    //si coincide lo vuelve a dibujar en la pantalla del cajero
                    lstProductos.Items.Add($"{p.Nombre} - ${p.Precio:N2} [Stock: {p.Stock}]");
                }
            }
        }

        #endregion

        #region MÉTODOS DE LÓGICA Y CÁLCULO

        private void CargarProductos()
        {
            lstProductos.Items.Clear();
            lstProductosAdmin.Items.Clear();
            listaDeProductos.Clear();

            //pido los datos puros al servicio sin saber que provienen de SQLite
            var productosDB = _inventarioService.ObtenerProductos();


                foreach (var p in productosDB)
                {
                    listaDeProductos.Add(p);
                    string lineaVisual = ($"{p.Nombre} - ${p.Precio:N2} [Stock: {p.Stock}]");

                    //alimenta de forma simultanea ambas pantallas del sistema
                    lstProductos.Items.Add(lineaVisual);
                    lstProductosAdmin.Items.Add(lineaVisual);
                }
            txtBuscar_TextChanged(null, null);
            
            ActualizarTotalInventario();
        }

        private void ActualizarTotalInventario()
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