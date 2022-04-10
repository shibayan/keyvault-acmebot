import punycode from 'punycode'

export const toUnicode = (value: string): string => {
  return punycode.toUnicode(value);
}

export const formatCreatedOn = (value: string): string => {
  return new Date(value).toLocaleString();
}

export const formatExpiresOn = (value: string): string => {
  const date = Date.parse(value);
  const diff = date - Date.now();
  const remainDays = Math.round(diff / (1000 * 60 * 60 * 24));

  const remainText = diff > 0 ? `Expires in ${remainDays} days` : `EXPIRED`;

  return `${date.toLocaleString()} (${remainText})`;
}
