// crypto.ts

// تولید کلید AES 256
export async function generateAESKey(): Promise<CryptoKey> {
  return crypto.subtle.generateKey(
    { name: "AES-GCM", length: 256 },
    true,
    ["encrypt", "decrypt"]
  );
}

// رمزنگاری متن با کلید AES و IV تصادفی
export async function encryptAES(key: CryptoKey, plainText: string): Promise<string> {
  const iv = crypto.getRandomValues(new Uint8Array(12));
  const encoded = new TextEncoder().encode(plainText);

  const cipherBuffer = await crypto.subtle.encrypt(
    { name: "AES-GCM", iv },
    key,
    encoded
  );

  // ترکیب IV و ciphertext
  const fullData = new Uint8Array(iv.length + cipherBuffer.byteLength);
  fullData.set(iv);
  fullData.set(new Uint8Array(cipherBuffer), iv.length);

  return btoa(String.fromCharCode(...fullData)); // خروجی base64
}

// رمزگشایی متن رمز‌شده AES
export async function decryptAES(key: CryptoKey, encryptedBase64: string): Promise<string> {
  const data = Uint8Array.from(atob(encryptedBase64), c => c.charCodeAt(0));
  const iv = data.slice(0, 12);
  const cipher = data.slice(12);

  const decryptedBuffer = await crypto.subtle.decrypt(
    { name: "AES-GCM", iv },
    key,
    cipher
  );

  return new TextDecoder().decode(decryptedBuffer);
}

// export کردن کلید AES به base64 برای ذخیره یا ارسال
export async function exportAESKey(key: CryptoKey): Promise<string> {
  const raw = await crypto.subtle.exportKey("raw", key);
  return btoa(String.fromCharCode(...new Uint8Array(raw)));
}

// import کردن کلید AES از base64
export async function importAESKey(base64: string): Promise<CryptoKey> {
  const raw = Uint8Array.from(atob(base64), c => c.charCodeAt(0));
  return crypto.subtle.importKey("raw", raw, "AES-GCM", true, ["encrypt", "decrypt"]);
}

export async function encryptWithPublicKey(pemPublicKey: string, data: Uint8Array): Promise<string> {
  const key = await importRSAPublicKey(pemPublicKey);
  const encrypted = await crypto.subtle.encrypt(
    { name: 'RSA-OAEP' },
    key,
    data
  );
  return btoa(String.fromCharCode(...new Uint8Array(encrypted)));
}

export async function decryptWithPrivateKey(pemPrivateKey: string, encryptedBase64: string): Promise<Uint8Array> {
  const encrypted = Uint8Array.from(atob(encryptedBase64), c => c.charCodeAt(0));
  const key = await importRSAPrivateKey(pemPrivateKey);
  const decrypted = await crypto.subtle.decrypt(
    { name: 'RSA-OAEP' },
    key,
    encrypted
  );
  return new Uint8Array(decrypted);
}

async function importRSAPublicKey(pem: string): Promise<CryptoKey> {
  const raw = pemToArrayBuffer(pem);
  return crypto.subtle.importKey('spki', raw, { name: 'RSA-OAEP', hash: 'SHA-256' }, true, ['encrypt']);
}

async function importRSAPrivateKey(pem: string): Promise<CryptoKey> {
  const raw = pemToArrayBuffer(pem);
  return crypto.subtle.importKey('pkcs8', raw, { name: 'RSA-OAEP', hash: 'SHA-256' }, true, ['decrypt']);
}

function pemToArrayBuffer(pem: string): ArrayBuffer {
  const b64 = pem.replace(/-----(BEGIN|END) [\w ]+-----/g, '').replace(/\s+/g, '');
  const binStr = atob(b64);
  const bytes = new Uint8Array(binStr.length);
  for (let i = 0; i < binStr.length; i++) {
    bytes[i] = binStr.charCodeAt(i);
  }
  return bytes.buffer;
}



export async function generateKeyPair(): Promise<{ publicKey: string; privateKey: string }> {
  const keyPair = await window.crypto.subtle.generateKey(
    {
      name: 'RSA-OAEP',
      modulusLength: 2048,
      publicExponent: new Uint8Array([1, 0, 1]),
      hash: 'SHA-256',
    },
    true,
    ['encrypt', 'decrypt']
  );

  const publicKey = await window.crypto.subtle.exportKey('spki', keyPair.publicKey);
  const privateKey = await window.crypto.subtle.exportKey('pkcs8', keyPair.privateKey);

  const publicPem = toPEM(publicKey, 'PUBLIC KEY');
  const privatePem = toPEM(privateKey, 'PRIVATE KEY');

  return { publicKey: publicPem, privateKey: privatePem };
}

function toPEM(raw: ArrayBuffer, type: string): string {
  const base64 = btoa(String.fromCharCode(...new Uint8Array(raw)));
  const formatted = base64.match(/.{1,64}/g)?.join('\n') ?? '';
  return `-----BEGIN ${type}-----\n${formatted}\n-----END ${type}-----`;
}
