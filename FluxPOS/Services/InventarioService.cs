using FluxPOS.Models;
using SQLite;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows.Documents;

namespace FluxPOS.Services
{
    class InventarioService
    {
        private readonly string rutaBaseDeDatos;
        //el constructor del servicio centraliza la inicializacion de las tablas
        public InventarioService()
        {
            // defino la ubicación del archivo de la base de datos de manera segura y fija 
            rutaBaseDeDatos = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "FluxPOS.db");

        //asegura la infraestructura fisica al instanciar el servicio
        using (SQLiteConnection conexion = new SQLiteConnection(rutaBaseDeDatos))
            {
                conexion.CreateTable<Producto>(); //tabla inventario
                conexion.CreateTable<Venta>(); //tabla para historial de ventas. Tickets (maestro)
                conexion.CreateTable<DetalleVenta>(); //renglones de Productos (tabla intermedia)
            }
        }

        public List<Producto> ObtenerProductos()
        {
            using (SQLiteConnection conexion = new SQLiteConnection(rutaBaseDeDatos))
            {
                return conexion.Table<Producto>().ToList();
            }
        }

        public void RegistrarProducto(Producto nuevoProducto)
        {
            using(SQLiteConnection conexion = new SQLiteConnection(rutaBaseDeDatos))
            {
                conexion.Insert(nuevoProducto);
            }
        }

        public void RestarUnidadStock(Producto producto)
        {
            if(producto.Stock > 0)
            {
                producto.Stock -= 1;
                using (SQLiteConnection conexion = new SQLiteConnection(rutaBaseDeDatos))
                {
                    conexion.Update(producto);
                }
            }
        }

        public void EliminarProducto (Producto producto)
        {
            //validar que haya algo en el carrito
            if (producto.Stock > 0)
            {
                producto.Stock -= 1;
                using (SQLiteConnection conexion = new SQLiteConnection(rutaBaseDeDatos))
                {
                    conexion.Update(producto);
                }
            }
        }

        public void FinalizarVenta(List<Producto> carrito, decimal totalVenta, out int ticketId)
        {
            //fabrica el objeto venta (ticket) con los datos listos
            Venta nuevaVenta = new Venta
            {
                Fecha = DateTime.Now,
                Total = totalVenta
            };

            using (SQLiteConnection conexion = new SQLiteConnection(rutaBaseDeDatos))
            {
                //guarda la cabecera de la venta
                conexion.Insert(nuevaVenta);
                ticketId = nuevaVenta.Id; //devuelve el ID generado para el cartel de exito

                /*procesar el stock y los detalles
                para no repetir el mismo producto, se crea un diccionario temporal en la RAM
                teniendo como clave la ID del Producto, el valor va a ser un objeto DetalleVenta agrupado
                */
                Dictionary<int, DetalleVenta> detallesAgrupados = new Dictionary<int, DetalleVenta>();
                foreach(Producto p in carrito) {
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
                foreach (var par in detallesAgrupados)
                {
                    DetalleVenta detalle = par.Value;
                    //graba de forma persistente el renglón en la tabla intermedia de SQLite
                    conexion.Insert(detalle); 

                    //ALGORITMO DE DESCUENTO: busca el producto real en el inventario físico mediante su ID
                    Producto prodInve = conexion.Table<Producto>().FirstOrDefault(x => x.Id == detalle.ProductoId);
                    if(prodInve != null)
                    {
                        //resta las unidades que el cliente compro del stock actual en la bd
                        prodInve.Stock -= detalle.Cantidad;
                        //actualiza el producto con su nuevo stock modificado
                        conexion.Update(prodInve);
                    }
                }
            }
        }
    }
}
