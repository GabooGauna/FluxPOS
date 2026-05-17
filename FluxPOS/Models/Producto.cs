using System;
using System.Collections.Generic;
using System.Text;
using SQLite;

namespace FluxPOS.Models
{
    class Producto
    {
        [PrimaryKey, AutoIncrement] //le dice a SQlite que no hay dos productos con el mismo ID y que lo haga autoincrementado
        public int Id { get; set; } //crear un id unico por producto

        public string Nombre { get; set; }
        public decimal Precio { get; set; }

        //controla las unidades disponibles en el negocio
        public int Stock { get; set; }
    }
}
