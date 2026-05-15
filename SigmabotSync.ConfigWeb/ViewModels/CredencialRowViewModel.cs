using CommunityToolkit.Mvvm.ComponentModel;
using SigmabotSync.Domain.Entities;

namespace SigmabotSync.ConfigWeb.ViewModels
{
    public partial class CredencialRowViewModel : ObservableObject
    {
        public static CredencialRowViewModel FromEntity(Credencial c)
        {
            if (c == null)
                return new CredencialRowViewModel();
            var vm = new CredencialRowViewModel();
            vm.Id = c.Id;
            vm.Nombre = c.Nombre;
            vm.Tipo = c.Tipo;
            vm.AconexInstancia = c.Aconex_Instancia;
            vm.AconexUsuario = c.Aconex_Usuario;
            vm.AconexClave = c.Aconex_Clave;
            vm.AconexIntegrationId = c.Aconex_IntegrationId;
            vm.AconexOrganizationId = c.Aconex_OrganizationId;
            vm.AconexUserId = c.Aconex_UserId;
            vm.BdServidor = c.BD_Servidor;
            vm.BdTipoConexion = c.BD_TipoConexion;
            vm.BdUsuario = c.BD_Usuario;
            vm.BdClave = c.BD_Clave;
            vm.BdBaseDatos = c.BD_BaseDatos;
            return vm;
        }

        public Credencial ToEntity()
        {
            return new Credencial
            {
                Id = Id,
                Nombre = Nombre,
                Tipo = Tipo,
                Aconex_Instancia = AconexInstancia,
                Aconex_Usuario = AconexUsuario,
                Aconex_Clave = AconexClave,
                Aconex_IntegrationId = AconexIntegrationId,
                Aconex_OrganizationId = AconexOrganizationId,
                Aconex_UserId = AconexUserId,
                BD_Servidor = BdServidor,
                BD_TipoConexion = BdTipoConexion,
                BD_Usuario = BdUsuario,
                BD_Clave = BdClave,
                BD_BaseDatos = BdBaseDatos
            };
        }

        [ObservableProperty] private int _id;
        [ObservableProperty] private string _nombre;
        [ObservableProperty] private string _tipo;
        [ObservableProperty] private string _aconexInstancia;
        [ObservableProperty] private string _aconexUsuario;
        [ObservableProperty] private string _aconexClave;
        [ObservableProperty] private string _aconexIntegrationId;
        [ObservableProperty] private string _aconexOrganizationId;
        [ObservableProperty] private string _aconexUserId;
        [ObservableProperty] private string _bdServidor;
        [ObservableProperty] private string _bdTipoConexion;
        [ObservableProperty] private string _bdUsuario;
        [ObservableProperty] private string _bdClave;
        [ObservableProperty] private string _bdBaseDatos;
    }
}
