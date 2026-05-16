using System;
using System.Collections.Generic;
using System.Text;
using SQLite;

namespace FluxPOS.Models
{
    internal class Venta
    {
        [PrimaryKey, AutoIncrement] //SQLite se encarga de que el ticket tenga id unico
        public int Id { get; set; }

        public DateTime Fecha { get; set; } //guarda el momento exacto de la transaccion

        public decimal Total { get; set; } //el monto final cobrado
    }
}
