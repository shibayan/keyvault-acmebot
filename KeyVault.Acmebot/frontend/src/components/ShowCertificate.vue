<script setup lang="ts">
import { ref } from 'vue';
import { Certificate } from '../types'
import { toUnicode, formatCreatedOn, formatExpiresOn } from '../utils'

defineProps<{ certificate: Certificate }>()

const sending = ref(false)
const modalActive = ref(true)

const renew = (): void => {

}

const revoke = (): void => {

}
</script>

<template>
  <div class="modal" :class="{ 'is-active': modalActive }">
    <div class="modal-background"></div>
    <div class="modal-card">
      <header class="modal-card-head">
        <p class="modal-card-title">Details certificate</p>
      </header>
      <section class="modal-card-body">
        <div class="field is-horizontal">
          <div class="field-label">
            <label class="label">Certificate Name</label>
          </div>
          <div class="field-body">
            <div class="content">
              {{ certificate.name }}
            </div>
          </div>
        </div>
        <div class="field is-horizontal">
          <div class="field-label is-normal">
            <label class="label">DNS Names</label>
          </div>
          <div class="field-body">
            <div class="content">
              <div class="tags">
                <span v-for="dnsName in certificate.dnsNames" class="tag is-light is-medium">
                  {{ toUnicode(dnsName) }}
                </span>
              </div>
            </div>
          </div>
        </div>
        <div class="field is-horizontal">
          <div class="field-label">
            <label class="label">Created On</label>
          </div>
          <div class="field-body">
            <div class="content">
              {{ formatCreatedOn(certificate.createdOn) }}
            </div>
          </div>
        </div>
        <div class="field is-horizontal">
          <div class="field-label">
            <label class="label">Expires On</label>
          </div>
          <div class="field-body">
            <div class="content">
              {{ formatExpiresOn(certificate.expiresOn) }}
            </div>
          </div>
        </div>
        <div class="field is-horizontal">
          <div class="field-label">
            <label class="label">X.509 Thumbprint</label>
          </div>
          <div class="field-body">
            <div class="content">
              {{ certificate.x509Thumbprint }}
            </div>
          </div>
        </div>
        <div class="field is-horizontal">
          <div class="field-label">
            <label class="label">Key Type</label>
          </div>
          <div class="field-body">
            <div class="content">
              {{ certificate.keyType }}
            </div>
          </div>
        </div>
        <div v-if="certificate.keyType === 'RSA'" class="field is-horizontal">
          <div class="field-label">
            <label class="label">Key Size</label>
          </div>
          <div class="field-body">
            <div class="content">
              {{ certificate.keySize }} bit
            </div>
          </div>
        </div>
        <div v-if="certificate.keyType === 'EC'" class="field is-horizontal">
          <div class="field-label">
            <label class="label">Elliptic Curve Name</label>
          </div>
          <div class="field-body">
            <div class="content">
              {{ certificate.keyCurveName }}
            </div>
          </div>
        </div>
        <div class="field is-horizontal">
          <div class="field-label">
            <label class="label">Reuse Key on Renewal?</label>
          </div>
          <div class="field-body">
            <div class="content">
              {{ certificate.reuseKey }}
            </div>
          </div>
        </div>
        <div class="field is-horizontal">
          <div class="field-label">
            <label class="label">Managed by Acmebot?</label>
          </div>
          <div class="field-body">
            <div class="content">
              {{ certificate.isManaged }}
            </div>
          </div>
        </div>
      </section>
      <footer class="modal-card-foot is-justify-content-flex-end">
        <button class="button is-primary" @click="renew(certificate)" :class="{ 'is-loading': sending }">Renew</button>
        <button class="button is-danger" @click="revoke(certificate)" :class="{ 'is-loading': sending }"
          v-if="certificate.isManaged && !certificate.isExpired">Revoke</button>
        <button class="button" @click="modalActive = false" :disabled="sending">Close</button>
      </footer>
    </div>
  </div>
</template>
