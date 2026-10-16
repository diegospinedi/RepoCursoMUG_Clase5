import type { InputHTMLAttributes, SelectHTMLAttributes } from 'react';

type PropsCampo = InputHTMLAttributes<HTMLInputElement> & {
  id: string;
  etiqueta: string;
  error?: string | undefined;
};

/**
 * Campo con su mensaje de error al lado (FR-17): el mensaje queda asociado al campo con `aria-describedby` y
 * el campo se marca con `aria-invalid`.
 */
export function CampoConError({ id, etiqueta, error, ...resto }: PropsCampo) {
  const idError = `${id}-error`;
  return (
    <div className="campo">
      <label htmlFor={id}>{etiqueta}</label>
      <input
        id={id}
        name={id}
        aria-invalid={error ? true : undefined}
        aria-describedby={error ? idError : undefined}
        {...resto}
      />
      {error ? (
        <p id={idError} className="campo__error">
          {error}
        </p>
      ) : null}
    </div>
  );
}

type PropsSeleccion = SelectHTMLAttributes<HTMLSelectElement> & {
  id: string;
  etiqueta: string;
  error?: string | undefined;
  opciones: ReadonlyArray<{ valor: string; texto: string }>;
};

/** Lista de opciones con el mismo tratamiento de errores que {@link CampoConError}. */
export function SeleccionConError({ id, etiqueta, error, opciones, ...resto }: PropsSeleccion) {
  const idError = `${id}-error`;
  return (
    <div className="campo">
      <label htmlFor={id}>{etiqueta}</label>
      <select
        id={id}
        name={id}
        aria-invalid={error ? true : undefined}
        aria-describedby={error ? idError : undefined}
        {...resto}
      >
        {opciones.map((opcion) => (
          <option key={opcion.valor} value={opcion.valor}>
            {opcion.texto}
          </option>
        ))}
      </select>
      {error ? (
        <p id={idError} className="campo__error">
          {error}
        </p>
      ) : null}
    </div>
  );
}
