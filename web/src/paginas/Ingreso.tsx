import { useState, type FormEvent } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import { ErrorApi, ErrorConexion, ingresar, MAXIMO_CONTRASENA, MENSAJE_SIN_CONEXION } from '../api/cliente';
import { CampoConError } from '../componentes/CampoConError';
import { Layout } from '../componentes/Layout';


function dosDigitos(valor: number): string {
  return valor.toString().padStart(2, '0');
}

/** Hora local HH:MM de una fecha ISO 8601, o null si no es una fecha válida. */
function horaLocal(iso: unknown): string | null {
  if (typeof iso !== 'string') {
    return null;
  }

  const fecha = new Date(iso);
  return Number.isNaN(fecha.getTime()) ? null : `${dosDigitos(fecha.getHours())}:${dosDigitos(fecha.getMinutes())}`;
}

function mensajeIntentos(restantes: unknown): string {
  if (typeof restantes !== 'number') {
    return 'La contraseña no es correcta.';
  }

  return restantes === 1
    ? 'La contraseña no es correcta. Te queda 1 intento.'
    : `La contraseña no es correcta. Te quedan ${restantes} intentos.`;
}

/** Pantalla de ingreso con la contraseña única. */
export function Ingreso() {
  const navegar = useNavigate();
  const ubicacion = useLocation();
  const aviso = (ubicacion.state as { aviso?: unknown } | null)?.aviso;

  const [contrasena, setContrasena] = useState('');
  const [errorCampo, setErrorCampo] = useState<string | undefined>();
  const [mensaje, setMensaje] = useState<string | null>(null);
  const [enviando, setEnviando] = useState(false);

  async function enviar(evento: FormEvent) {
    evento.preventDefault();
    // La contraseña solo vive en el estado del formulario y se limpia en cada envío.
    const valor = contrasena;
    setContrasena('');
    setErrorCampo(undefined);
    setMensaje(null);
    setEnviando(true);

    try {
      await ingresar(valor);
      navegar('/configuracion', { replace: true });
    } catch (causa) {
      if (causa instanceof ErrorApi) {
        if (causa.estado === 401) {
          setMensaje(mensajeIntentos(causa.extensiones.intentosRestantes));
        } else if (causa.estado === 423) {
          const hora = horaLocal(causa.extensiones.bloqueadoHasta);
          setMensaje(hora ? `Ingreso bloqueado hasta ${hora}` : 'Ingreso bloqueado. Intentá más tarde.');
        } else if (causa.estado === 409) {
          navegar('/crear-contrasena', { replace: true });
        } else if (causa.campos.contrasena) {
          setErrorCampo(causa.campos.contrasena);
        } else {
          setMensaje(causa.titulo);
        }
      } else if (causa instanceof ErrorConexion) {
        setMensaje(causa.message);
      } else {
        setMensaje(MENSAJE_SIN_CONEXION);
      }
    } finally {
      setEnviando(false);
    }
  }

  return (
    <Layout>
      <h1>Ingresar</h1>
      {typeof aviso === 'string' ? (
        <p className="mensaje" role="status">
          {aviso}
        </p>
      ) : null}
      {mensaje ? (
        <p className="mensaje mensaje--error" role="alert">
          {mensaje}
        </p>
      ) : null}
      <form onSubmit={enviar} noValidate>
        <CampoConError
          id="contrasena"
          etiqueta="Contraseña"
          type="password"
          autoComplete="current-password"
          maxLength={MAXIMO_CONTRASENA}
          value={contrasena}
          error={errorCampo}
          onChange={(evento) => setContrasena(evento.target.value)}
        />
        <button type="submit" className="boton" disabled={enviando}>
          Ingresar
        </button>
      </form>
    </Layout>
  );
}
