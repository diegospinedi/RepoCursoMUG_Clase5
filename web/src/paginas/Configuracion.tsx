import { useEffect, useState, type FormEvent } from 'react';
import {
  cambiarContrasena,
  ErrorApi,
  ErrorConexion,
  ErrorSesionVencida,
  guardarConfiguracion,
  MAXIMO_CONTRASENA,
  MENSAJE_SIN_CONEXION,
  obtenerConfiguracion,
  type CondicionFiscal,
  type ConfiguracionNegocio,
} from '../api/cliente';
import { CampoConError, SeleccionConError } from '../componentes/CampoConError';
import { Layout } from '../componentes/Layout';

const NO_ES_NUMERO = 'Ingresá un número';
const USA_COMA = 'Usá coma para los decimales';
const NO_COINCIDEN = 'Las contraseñas no coinciden';

const CONDICIONES: ReadonlyArray<{ valor: CondicionFiscal; texto: string }> = [
  { valor: 'ResponsableInscripto', texto: 'Responsable Inscripto' },
  { valor: 'Monotributo', texto: 'Monotributo' },
];

type CampoConfiguracion = keyof ConfiguracionNegocio;

interface Valores {
  alicuotaIva: string;
  condicionFiscal: CondicionFiscal;
  topeIdentificacion: string;
  multiploRedondeo: string;
}

const CAMPOS_CONFIGURACION: readonly CampoConfiguracion[] = [
  'alicuotaIva',
  'condicionFiscal',
  'topeIdentificacion',
  'multiploRedondeo',
];

// Número en formato es-AR: coma decimal y, opcionalmente, punto como separador de miles ("1.234,5"). El primer
// grupo de miles no puede empezar con 0: "0.050" no es "50", es un decimal escrito con punto.
const NUMERO_ES_AR = /^-?(?:\d+|[1-9]\d{0,2}(?:\.\d{3})+)(?:,\d+)?$/;
// Lo que parece un decimal escrito con punto ("0.050", "10.5"), sin ser un número es-AR válido.
const DECIMAL_CON_PUNTO = /^-?\d+\.\d+$/;

type LecturaNumero = { numero: number } | { error: string };

/** Convierte el texto ingresado a número, o explica por qué no es un número en formato es-AR. */
function leerNumero(texto: string): LecturaNumero {
  const limpio = texto.trim();
  if (!NUMERO_ES_AR.test(limpio)) {
    return { error: DECIMAL_CON_PUNTO.test(limpio) ? USA_COMA : NO_ES_NUMERO };
  }

  const numero = Number(limpio.replace(/\./g, '').replace(',', '.'));
  return Number.isFinite(numero) ? { numero } : { error: NO_ES_NUMERO };
}

/** Número a texto con coma decimal, sin separador de miles (es lo que la operadora vuelve a editar). */
function escribirNumero(numero: number): string {
  return String(numero).replace('.', ',');
}

function aValores(configuracion: ConfiguracionNegocio): Valores {
  return {
    alicuotaIva: escribirNumero(configuracion.alicuotaIva),
    condicionFiscal: configuracion.condicionFiscal,
    topeIdentificacion: configuracion.topeIdentificacion === null ? '' : escribirNumero(configuracion.topeIdentificacion),
    multiploRedondeo: escribirNumero(configuracion.multiploRedondeo),
  };
}

function mensajeDeFalla(causa: unknown): string {
  return causa instanceof ErrorApi ? causa.titulo : MENSAJE_SIN_CONEXION;
}

/** Parámetros del negocio (FR-07 a FR-14) y cambio de contraseña (FR-05). */
export function Configuracion() {
  const [valores, setValores] = useState<Valores | null>(null);
  const [errores, setErrores] = useState<Partial<Record<CampoConfiguracion, string>>>({});
  const [mensaje, setMensaje] = useState<string | null>(null);
  const [guardado, setGuardado] = useState(false);
  const [grabando, setGrabando] = useState(false);

  useEffect(() => {
    let activa = true;
    obtenerConfiguracion()
      .then((configuracion) => {
        if (activa) {
          setValores(aValores(configuracion));
        }
      })
      .catch((causa: unknown) => {
        if (activa && !(causa instanceof ErrorSesionVencida)) {
          setMensaje(mensajeDeFalla(causa));
        }
      });
    return () => {
      activa = false;
    };
  }, []);

  function cambiar<K extends keyof Valores>(campo: K, valor: Valores[K]) {
    setValores((anteriores) => (anteriores ? { ...anteriores, [campo]: valor } : anteriores));
    setGuardado(false);
  }

  async function grabar(evento: FormEvent) {
    evento.preventDefault();
    if (!valores) {
      return;
    }

    setMensaje(null);
    setGuardado(false);

    const alicuota = leerNumero(valores.alicuotaIva);
    const multiplo = leerNumero(valores.multiploRedondeo);
    const tope: LecturaNumero | null =
      valores.topeIdentificacion.trim() === '' ? null : leerNumero(valores.topeIdentificacion);

    const erroresLocales: Partial<Record<CampoConfiguracion, string>> = {};
    if ('error' in alicuota) {
      erroresLocales.alicuotaIva = alicuota.error;
    }
    if (tope && 'error' in tope) {
      erroresLocales.topeIdentificacion = tope.error;
    }
    if ('error' in multiplo) {
      erroresLocales.multiploRedondeo = multiplo.error;
    }

    setErrores(erroresLocales);
    if ('error' in alicuota || 'error' in multiplo || (tope && 'error' in tope)) {
      return;
    }

    setGrabando(true);
    try {
      const guardada = await guardarConfiguracion({
        alicuotaIva: alicuota.numero,
        condicionFiscal: valores.condicionFiscal,
        topeIdentificacion: tope ? tope.numero : null,
        multiploRedondeo: multiplo.numero,
      });
      setValores(aValores(guardada));
      setGuardado(true);
    } catch (causa) {
      if (causa instanceof ErrorSesionVencida) {
        return;
      }

      if (causa instanceof ErrorApi) {
        const porCampo: Partial<Record<CampoConfiguracion, string>> = {};
        const otros: string[] = [];
        for (const [campo, texto] of Object.entries(causa.campos)) {
          if ((CAMPOS_CONFIGURACION as readonly string[]).includes(campo)) {
            porCampo[campo as CampoConfiguracion] = texto;
          } else {
            otros.push(texto);
          }
        }
        setErrores(porCampo);
        if (otros.length > 0 || Object.keys(porCampo).length === 0) {
          setMensaje([causa.titulo, ...otros].join('. '));
        }
      } else {
        // Red caída o 5xx: lo cargado queda en el formulario para volver a intentar.
        setMensaje(causa instanceof ErrorConexion ? causa.message : MENSAJE_SIN_CONEXION);
      }
    } finally {
      setGrabando(false);
    }
  }

  return (
    <Layout conSesion>
      <h1>Configuración</h1>
      {mensaje ? (
        <p className="mensaje mensaje--error" role="alert">
          {mensaje}
        </p>
      ) : null}
      {guardado ? (
        <p className="mensaje" role="status">
          Configuración guardada
        </p>
      ) : null}
      {valores ? (
        <form onSubmit={grabar} noValidate>
          <CampoConError
            id="alicuotaIva"
            etiqueta="Alícuota de IVA (%)"
            inputMode="decimal"
            autoComplete="off"
            value={valores.alicuotaIva}
            error={errores.alicuotaIva}
            onChange={(evento) => cambiar('alicuotaIva', evento.target.value)}
          />
          <SeleccionConError
            id="condicionFiscal"
            etiqueta="Condición fiscal"
            opciones={CONDICIONES}
            value={valores.condicionFiscal}
            error={errores.condicionFiscal}
            onChange={(evento) => cambiar('condicionFiscal', evento.target.value as CondicionFiscal)}
          />
          <CampoConError
            id="topeIdentificacion"
            etiqueta="Tope de identificación del receptor"
            inputMode="decimal"
            autoComplete="off"
            placeholder="Sin cargar"
            value={valores.topeIdentificacion}
            error={errores.topeIdentificacion}
            onChange={(evento) => cambiar('topeIdentificacion', evento.target.value)}
          />
          <CampoConError
            id="multiploRedondeo"
            etiqueta="Múltiplo de redondeo"
            inputMode="decimal"
            autoComplete="off"
            value={valores.multiploRedondeo}
            error={errores.multiploRedondeo}
            onChange={(evento) => cambiar('multiploRedondeo', evento.target.value)}
          />
          <button type="submit" className="boton" disabled={grabando}>
            Grabar
          </button>
        </form>
      ) : mensaje ? null : (
        <p role="status">Cargando…</p>
      )}
      <CambioContrasena />
    </Layout>
  );
}

interface ErroresContrasena {
  contrasenaActual?: string | undefined;
  contrasenaNueva?: string | undefined;
  confirmacionNueva?: string | undefined;
}

/** Sección "Cambiar contraseña" (FR-05). */
function CambioContrasena() {
  const [actual, setActual] = useState('');
  const [nueva, setNueva] = useState('');
  const [repetida, setRepetida] = useState('');
  const [errores, setErrores] = useState<ErroresContrasena>({});
  const [mensaje, setMensaje] = useState<string | null>(null);
  const [cambiada, setCambiada] = useState(false);
  const [enviando, setEnviando] = useState(false);

  async function enviar(evento: FormEvent) {
    evento.preventDefault();
    // Las contraseñas solo viven en el estado del formulario y se limpian en cada envío.
    const valorActual = actual;
    const valorNueva = nueva;
    const valorRepetida = repetida;
    setActual('');
    setNueva('');
    setRepetida('');
    setErrores({});
    setMensaje(null);
    setCambiada(false);

    if (valorNueva !== valorRepetida) {
      setErrores({ confirmacionNueva: NO_COINCIDEN });
      return;
    }

    setEnviando(true);
    try {
      await cambiarContrasena(valorActual, valorNueva);
      setCambiada(true);
    } catch (causa) {
      if (causa instanceof ErrorSesionVencida) {
        return;
      }

      if (causa instanceof ErrorApi && (causa.campos.contrasenaActual || causa.campos.contrasenaNueva)) {
        setErrores({ contrasenaActual: causa.campos.contrasenaActual, contrasenaNueva: causa.campos.contrasenaNueva });
      } else {
        setMensaje(mensajeDeFalla(causa));
      }
    } finally {
      setEnviando(false);
    }
  }

  return (
    <section className="seccion" aria-labelledby="titulo-cambio-contrasena">
      <h2 id="titulo-cambio-contrasena">Cambiar contraseña</h2>
      {mensaje ? (
        <p className="mensaje mensaje--error" role="alert">
          {mensaje}
        </p>
      ) : null}
      {cambiada ? (
        <p className="mensaje" role="status">
          La contraseña se cambió
        </p>
      ) : null}
      <form onSubmit={enviar} noValidate>
        <CampoConError
          id="contrasenaActual"
          etiqueta="Contraseña actual"
          type="password"
          autoComplete="current-password"
          maxLength={MAXIMO_CONTRASENA}
          value={actual}
          error={errores.contrasenaActual}
          onChange={(evento) => setActual(evento.target.value)}
        />
        <CampoConError
          id="contrasenaNueva"
          etiqueta="Contraseña nueva"
          type="password"
          autoComplete="new-password"
          maxLength={MAXIMO_CONTRASENA}
          value={nueva}
          error={errores.contrasenaNueva}
          onChange={(evento) => setNueva(evento.target.value)}
        />
        <CampoConError
          id="confirmacionNueva"
          etiqueta="Repetí la contraseña nueva"
          type="password"
          autoComplete="new-password"
          maxLength={MAXIMO_CONTRASENA}
          value={repetida}
          error={errores.confirmacionNueva}
          onChange={(evento) => setRepetida(evento.target.value)}
        />
        <button type="submit" className="boton" disabled={enviando}>
          Cambiar contraseña
        </button>
      </form>
    </section>
  );
}
