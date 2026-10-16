import { useState, type FormEvent } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  crearContrasenaInicial,
  ErrorApi,
  ErrorConexion,
  MAXIMO_CONTRASENA,
  MENSAJE_SIN_CONEXION,
} from '../api/cliente';
import { CampoConError } from '../componentes/CampoConError';
import { Layout } from '../componentes/Layout';

const NO_COINCIDEN = 'Las contraseñas no coinciden';

interface Errores {
  contrasena?: string | undefined;
  confirmacion?: string | undefined;
}

/** Creación de la contraseña cuando todavía no existe (FR-02, FR-03, FR-04). */
export function CrearContrasena() {
  const navegar = useNavigate();
  const [contrasena, setContrasena] = useState('');
  const [confirmacion, setConfirmacion] = useState('');
  const [errores, setErrores] = useState<Errores>({});
  const [mensaje, setMensaje] = useState<string | null>(null);
  const [enviando, setEnviando] = useState(false);

  async function enviar(evento: FormEvent) {
    evento.preventDefault();
    // Las contraseñas solo viven en el estado del formulario y se limpian en cada envío.
    const valor = contrasena;
    const repetida = confirmacion;
    setContrasena('');
    setConfirmacion('');
    setErrores({});
    setMensaje(null);

    if (valor !== repetida) {
      setErrores({ confirmacion: NO_COINCIDEN });
      return;
    }

    setEnviando(true);
    try {
      await crearContrasenaInicial(valor);
      navegar('/ingreso', { replace: true, state: { aviso: 'La contraseña se creó. Ingresá con ella.' } });
    } catch (causa) {
      if (causa instanceof ErrorApi && causa.estado === 409) {
        navegar('/ingreso', { replace: true, state: { aviso: 'La contraseña ya estaba definida' } });
      } else if (causa instanceof ErrorApi && causa.campos.contrasena) {
        setErrores({ contrasena: causa.campos.contrasena });
      } else if (causa instanceof ErrorApi) {
        setMensaje(causa.titulo);
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
      <h1>Crear la contraseña</h1>
      <p>Todavía no hay una contraseña. Elegí una de al menos 8 caracteres.</p>
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
          autoComplete="new-password"
          maxLength={MAXIMO_CONTRASENA}
          value={contrasena}
          error={errores.contrasena}
          onChange={(evento) => setContrasena(evento.target.value)}
        />
        <CampoConError
          id="confirmacion"
          etiqueta="Repetí la contraseña"
          type="password"
          autoComplete="new-password"
          maxLength={MAXIMO_CONTRASENA}
          value={confirmacion}
          error={errores.confirmacion}
          onChange={(evento) => setConfirmacion(evento.target.value)}
        />
        <button type="submit" className="boton" disabled={enviando}>
          Crear contraseña
        </button>
      </form>
    </Layout>
  );
}
