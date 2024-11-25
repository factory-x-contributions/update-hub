resource "aws_acm_certificate" "certificate" {
  domain_name = "*.factory-x.${var.domain_name}"
  validation_method = "DNS"

  lifecycle {
    create_before_destroy = true
  }
}

resource "aws_route53_record" "certificate_record" {
  for_each = {
    for dvo in aws_acm_certificate.certificate.domain_validation_options : dvo.domain_name => {
      name   = dvo.resource_record_name
      record = dvo.resource_record_value
      type   = dvo.resource_record_type
    }
  }

  allow_overwrite = true
  name            = each.value.name
  records         = [each.value.record]
  ttl             = 60
  type            = each.value.type
  zone_id         = aws_route53_zone.irs_zone.zone_id
}

resource "aws_acm_certificate_validation" "certificate_validation" {
  certificate_arn         = aws_acm_certificate.certificate.arn
  validation_record_fqdns = [for record in aws_route53_record.certificate_record : record.fqdn]
}

data "aws_route53_zone" "existing" {
  name = var.domain_name
}

resource "aws_route53_record" "subdomain-ns" {
  zone_id = data.aws_route53_zone.existing.zone_id
  name    = "factory-x.${var.domain_name}"
  type    = "NS"
  ttl     = "300"
  records = aws_route53_zone.irs_zone.name_servers
}
