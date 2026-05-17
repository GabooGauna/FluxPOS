using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using SQLite;

namespace FluxPOS.Models
{
    class DetalleVenta
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        //relacion: almacena el ID del ticket general al que pertenece este renglon
        [Indexed]
        public int VentaId { get; set; }

        //relacion: almacena el ID del producto que se vendio
        public int ProductoId { get; set; }

        //datos historicos fijados en el momento de la transaccion
        public string NombreProducto { get; set; } = string.Empty;
        public int Cantidad { get; set; }
        public decimal PrecioCobrado { get; set; }
    }
}
